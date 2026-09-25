// Native speech for Mechanic Scope on iOS: recognition (Speech framework) for
// IOSVoiceRecognizer.cs and text-to-speech (AVSpeechSynthesizer) for VoiceFeedback.cs.
//
// Recognition results go back to Unity with UnitySendMessage in the format parsed by
// NativeSpeechMessage.cs:
//   OnNativeSpeechResult   "confidence|text"  (confidence -1 when unknown)
//   OnNativePartialResult  "text"
//   OnNativeSpeechError    "code|message"     (no_speech, permission, unavailable, network, other)
//   OnNativeListeningEnded ""                 (stopped on its own; not sent after _MSSpeechStop)
//
// iOS does not end an utterance by itself, so a short silence after the last partial result
// ends it. In continuous mode a new recognition task starts after each utterance; this also
// keeps each task well under the Speech framework's roughly one-minute limit.
//
// Requires Speech.framework and the NSSpeechRecognitionUsageDescription and
// NSMicrophoneUsageDescription Info.plist keys; VoiceBuildPostprocessor.cs adds all three.

#import <Foundation/Foundation.h>
#import <AVFoundation/AVFoundation.h>
#import <Speech/Speech.h>

extern "C" void UnitySendMessage(const char* obj, const char* method, const char* msg);

static const NSTimeInterval kEndOfUtteranceSilence = 1.2;
static const NSTimeInterval kRestartDelay = 0.25;

static NSString* MSString(const char* s)
{
    return s ? [NSString stringWithUTF8String:s] : @"";
}

@interface MSSpeechBridge : NSObject <AVSpeechSynthesizerDelegate>
@property (nonatomic, copy) NSString* unityObject;
@property (nonatomic, copy) NSString* languageTag;
@property (nonatomic) BOOL allowCloud;
@property (nonatomic) BOOL continuous;
@property (nonatomic) BOOL active;
@property (nonatomic) NSUInteger session;     // bumped on start/stop: callbacks from an older session are dropped
@property (nonatomic) NSUInteger utterance;   // bumped per recognition task: a cancelled task's late callback is dropped
@property (nonatomic, strong) SFSpeechRecognizer* recognizer;
@property (nonatomic, strong) SFSpeechAudioBufferRecognitionRequest* request;
@property (nonatomic, strong) SFSpeechRecognitionTask* task;
@property (nonatomic, strong) AVAudioEngine* audioEngine;
@property (nonatomic, strong) NSTimer* silenceTimer;
@property (nonatomic, strong) AVSpeechSynthesizer* synthesizer;
@end

@implementation MSSpeechBridge

+ (instancetype)shared
{
    static MSSpeechBridge* instance;
    static dispatch_once_t once;
    dispatch_once(&once, ^{ instance = [[MSSpeechBridge alloc] init]; });
    return instance;
}

- (instancetype)init
{
    if ((self = [super init]))
    {
        _audioEngine = [[AVAudioEngine alloc] init];
        _synthesizer = [[AVSpeechSynthesizer alloc] init];
        _synthesizer.delegate = self;
    }
    return self;
}

// === Messaging ===

- (void)send:(const char*)method message:(NSString*)message
{
    if (self.unityObject.length == 0) return;
    UnitySendMessage(self.unityObject.UTF8String, method, (message ?: @"").UTF8String);
}

- (void)failWithCode:(NSString*)code message:(NSString*)message
{
    [self send:"OnNativeSpeechError" message:[NSString stringWithFormat:@"%@|%@", code, message]];
    [self finish];
}

- (void)finish
{
    if (!self.active) return;
    self.active = NO;
    self.session++;
    [self teardownAudio];
    [self send:"OnNativeListeningEnded" message:@""];
}

// === Availability ===

- (SFSpeechRecognizer*)recognizerFor:(NSString*)languageTag
{
    NSLocale* locale = [NSLocale localeWithLocaleIdentifier:languageTag.length ? languageTag : @"en-US"];
    return [[SFSpeechRecognizer alloc] initWithLocale:locale];
}

- (BOOL)isAvailableFor:(NSString*)languageTag allowCloud:(BOOL)allowCloud
{
    SFSpeechRecognizer* recognizer = [self recognizerFor:languageTag];
    if (recognizer == nil) return NO;
    if (allowCloud) return YES;
    if (@available(iOS 13.0, *)) return recognizer.supportsOnDeviceRecognition;
    return NO;
}

// === Recognition ===

- (void)startWithObject:(NSString*)object language:(NSString*)language
             allowCloud:(BOOL)allowCloud continuous:(BOOL)continuous
{
    [self stopSilently];

    self.unityObject = object;
    self.languageTag = language;
    self.allowCloud = allowCloud;
    self.continuous = continuous;
    self.active = YES;
    NSUInteger session = ++self.session;

    [SFSpeechRecognizer requestAuthorization:^(SFSpeechRecognizerAuthorizationStatus status) {
        dispatch_async(dispatch_get_main_queue(), ^{
            if (!self.active || session != self.session) return;
            if (status != SFSpeechRecognizerAuthorizationStatusAuthorized)
            {
                [self failWithCode:@"permission" message:@"Speech recognition permission is required"];
                return;
            }

            [[AVAudioSession sharedInstance] requestRecordPermission:^(BOOL granted) {
                dispatch_async(dispatch_get_main_queue(), ^{
                    if (!self.active || session != self.session) return;
                    if (!granted)
                    {
                        [self failWithCode:@"permission" message:@"Microphone permission is required"];
                        return;
                    }
                    [self beginUtterance:session];
                });
            }];
        });
    }];
}

- (void)beginUtterance:(NSUInteger)session
{
    if (!self.active || session != self.session) return;

    self.recognizer = [self recognizerFor:self.languageTag];
    if (self.recognizer == nil || !self.recognizer.isAvailable)
    {
        [self failWithCode:@"unavailable" message:@"Speech recognition is not available right now"];
        return;
    }

    BOOL onDevice = NO;
    if (@available(iOS 13.0, *)) onDevice = self.recognizer.supportsOnDeviceRecognition;
    if (!onDevice && !self.allowCloud)
    {
        [self failWithCode:@"unavailable"
                   message:@"On-device speech recognition is not supported for this language on this device"];
        return;
    }

    NSError* error = nil;
    AVAudioSession* audioSession = [AVAudioSession sharedInstance];
    [audioSession setCategory:AVAudioSessionCategoryPlayAndRecord
                         mode:AVAudioSessionModeDefault
                      options:AVAudioSessionCategoryOptionDefaultToSpeaker |
                              AVAudioSessionCategoryOptionAllowBluetooth |
                              AVAudioSessionCategoryOptionMixWithOthers
                        error:&error];
    if (error == nil) [audioSession setActive:YES error:&error];
    if (error != nil)
    {
        [self failWithCode:@"other" message:[NSString stringWithFormat:@"Audio session error: %@",
                                             error.localizedDescription]];
        return;
    }

    NSUInteger utterance = ++self.utterance;

    SFSpeechAudioBufferRecognitionRequest* request = [[SFSpeechAudioBufferRecognitionRequest alloc] init];
    request.shouldReportPartialResults = YES;
    if (@available(iOS 13.0, *))
    {
        if (onDevice) request.requiresOnDeviceRecognition = YES;
    }
    self.request = request;

    AVAudioInputNode* input = self.audioEngine.inputNode;
    AVAudioFormat* format = [input outputFormatForBus:0];
    [input removeTapOnBus:0];
    [input installTapOnBus:0 bufferSize:1024 format:format block:^(AVAudioPCMBuffer* buffer, AVAudioTime* when) {
        [request appendAudioPCMBuffer:buffer];
    }];

    [self.audioEngine prepare];
    if (![self.audioEngine startAndReturnError:&error])
    {
        [self failWithCode:@"other" message:[NSString stringWithFormat:@"Microphone error: %@",
                                             error.localizedDescription]];
        return;
    }

    self.task = [self.recognizer recognitionTaskWithRequest:request
                                              resultHandler:^(SFSpeechRecognitionResult* result, NSError* taskError) {
        dispatch_async(dispatch_get_main_queue(), ^{
            [self handleResult:result error:taskError session:session utterance:utterance];
        });
    }];
}

- (void)handleResult:(SFSpeechRecognitionResult*)result error:(NSError*)error
             session:(NSUInteger)session utterance:(NSUInteger)utterance
{
    if (!self.active || session != self.session || utterance != self.utterance) return;

    if (result != nil && !result.isFinal)
    {
        NSString* text = result.bestTranscription.formattedString;
        if (text.length > 0)
        {
            [self send:"OnNativePartialResult" message:text];
            [self restartSilenceTimer:session utterance:utterance];
        }
        return;
    }

    [self.silenceTimer invalidate];
    self.silenceTimer = nil;

    BOOL heardSomething = NO;
    if (result != nil)
    {
        NSString* text = result.bestTranscription.formattedString;
        if (text.length > 0)
        {
            heardSomething = YES;
            [self send:"OnNativeSpeechResult"
               message:[NSString stringWithFormat:@"%.3f|%@", [self confidenceOf:result], text]];
        }
    }

    [self teardownAudio];

    if (error != nil && !heardSomething && !self.continuous)
    {
        // With no speech the task ends with an error; that is "nothing heard", not a failure.
        [self failWithCode:@"no_speech" message:@"No speech was recognized"];
        return;
    }

    if (self.continuous)
    {
        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(kRestartDelay * NSEC_PER_SEC)),
                       dispatch_get_main_queue(), ^{ [self beginUtterance:session]; });
    }
    else if (heardSomething)
    {
        [self finish];
    }
    else
    {
        [self failWithCode:@"no_speech" message:@"No speech was recognized"];
    }
}

- (double)confidenceOf:(SFSpeechRecognitionResult*)result
{
    NSArray<SFTranscriptionSegment*>* segments = result.bestTranscription.segments;
    if (segments.count == 0) return -1;

    double total = 0;
    for (SFTranscriptionSegment* segment in segments) total += segment.confidence;
    double average = total / segments.count;
    return average > 0 ? average : -1;
}

- (void)restartSilenceTimer:(NSUInteger)session utterance:(NSUInteger)utterance
{
    [self.silenceTimer invalidate];
    self.silenceTimer = [NSTimer scheduledTimerWithTimeInterval:kEndOfUtteranceSilence repeats:NO
                                                          block:^(NSTimer* timer) {
        if (self.active && session == self.session && utterance == self.utterance)
        {
            // Ends the audio; the task then delivers a final result.
            [self.request endAudio];
        }
    }];
}

- (void)teardownAudio
{
    // Invalidates any callback still in flight from the task being torn down.
    self.utterance++;

    [self.silenceTimer invalidate];
    self.silenceTimer = nil;

    if (self.audioEngine.isRunning) [self.audioEngine stop];
    [self.audioEngine.inputNode removeTapOnBus:0];

    [self.request endAudio];
    self.request = nil;
    [self.task cancel];
    self.task = nil;
}

- (void)stopSilently
{
    self.active = NO;
    self.session++;
    [self teardownAudio];
}

// === Text to speech ===

- (void)speak:(NSString*)text rate:(float)rate pitch:(float)pitch volume:(float)volume language:(NSString*)language
{
    AVSpeechUtterance* utterance = [AVSpeechUtterance speechUtteranceWithString:text];
    // VoiceFeedback's rate is 1.0 = normal; AVSpeechUtterance's normal is AVSpeechUtteranceDefaultSpeechRate.
    float scaled = AVSpeechUtteranceDefaultSpeechRate * rate;
    utterance.rate = MAX(AVSpeechUtteranceMinimumSpeechRate, MIN(AVSpeechUtteranceMaximumSpeechRate, scaled));
    utterance.pitchMultiplier = MAX(0.5f, MIN(2.0f, pitch));
    utterance.volume = MAX(0.0f, MIN(1.0f, volume));

    AVSpeechSynthesisVoice* voice = [AVSpeechSynthesisVoice voiceWithLanguage:language];
    if (voice != nil) utterance.voice = voice;

    [self.synthesizer speakUtterance:utterance];
}

@end

// === C entry points (see the DllImports in VoiceRecognizers.cs and VoiceFeedback.cs) ===
// Unity runs scripts on the iOS main thread, so these call straight through. That matters for
// _MSSpeak: VoiceFeedback polls _MSIsSpeaking immediately afterwards, and an async hop would let
// it read "not speaking" before speech started.

extern "C"
{
    int _MSSpeechIsAvailable(const char* languageTag, int allowCloud)
    {
        return [[MSSpeechBridge shared] isAvailableFor:MSString(languageTag) allowCloud:allowCloud != 0] ? 1 : 0;
    }

    void _MSSpeechStart(const char* gameObjectName, const char* languageTag, int allowCloud, int continuous)
    {
        [[MSSpeechBridge shared] startWithObject:MSString(gameObjectName) language:MSString(languageTag)
                                      allowCloud:allowCloud != 0 continuous:continuous != 0];
    }

    void _MSSpeechStop()
    {
        [[MSSpeechBridge shared] stopSilently];
    }

    void _MSSpeak(const char* text, float rate, float pitch, float volume, const char* language)
    {
        [[MSSpeechBridge shared] speak:MSString(text) rate:rate pitch:pitch volume:volume language:MSString(language)];
    }

    void _MSStopSpeaking()
    {
        [[MSSpeechBridge shared].synthesizer stopSpeakingAtBoundary:AVSpeechBoundaryImmediate];
    }

    int _MSIsSpeaking()
    {
        return [MSSpeechBridge shared].synthesizer.isSpeaking ? 1 : 0;
    }
}
