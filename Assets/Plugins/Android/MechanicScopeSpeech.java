package com.mechanicscope;

import android.app.Activity;
import android.content.Intent;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.speech.RecognitionListener;
import android.speech.RecognizerIntent;
import android.speech.SpeechRecognizer;

import com.unity3d.player.UnityPlayer;

import java.util.ArrayList;
import java.util.Locale;

/**
 * Bridge between Unity (AndroidVoiceRecognizer.cs) and android.speech.SpeechRecognizer.
 *
 * SpeechRecognizer must be created and called on the application's main thread, so every entry
 * point hops onto it. Results go back to Unity with UnitySendMessage in the format parsed by
 * NativeSpeechMessage.cs:
 *   OnNativeSpeechResult   "confidence|text"  (confidence -1 when the recognizer gives none)
 *   OnNativePartialResult  "text"
 *   OnNativeSpeechError    "code|message"     (no_speech, permission, unavailable, network, other)
 *   OnNativeListeningEnded ""                 (stopped on its own; not sent after stop())
 *
 * In continuous mode the recognizer is restarted after each utterance, and after silence, until
 * stop() is called. Android's recognizer only ever hears one utterance per startListening().
 */
public final class MechanicScopeSpeech {

    private static final long RESTART_DELAY_MS = 250;

    private static final Handler MAIN = new Handler(Looper.getMainLooper());

    private static SpeechRecognizer recognizer;
    private static boolean recognizerIsOnDevice;
    private static String unityObject;
    private static String languageTag;
    private static boolean allowCloud;
    private static boolean continuous;

    // Only touched on the main thread. Bumped on every start/stop so callbacks from a session
    // that was already stopped are recognised as stale and dropped.
    private static boolean active;
    private static int session;

    private MechanicScopeSpeech() {
    }

    /** Whether any speech recognition service is installed. Safe to call from any thread. */
    public static boolean isAvailable(Activity activity) {
        return SpeechRecognizer.isRecognitionAvailable(activity);
    }

    public static void start(final Activity activity, final String gameObject, final String language,
                             final boolean cloudAllowed, final boolean keepListening) {
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                unityObject = gameObject;
                languageTag = language;
                allowCloud = cloudAllowed;
                continuous = keepListening;
                active = true;
                session++;

                if (!ensureRecognizer(activity)) {
                    finishWithError("unavailable", "Speech recognition is not available on this device");
                    return;
                }
                listen(session);
            }
        });
    }

    public static void stop(Activity activity) {
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                active = false;
                session++;
                if (recognizer != null) {
                    recognizer.cancel();
                }
            }
        });
    }

    public static void destroy(Activity activity) {
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                active = false;
                session++;
                if (recognizer != null) {
                    recognizer.destroy();
                    recognizer = null;
                }
            }
        });
    }

    /**
     * Creates the recognizer. On Android 12+ an on-device recognizer is used when one exists, so
     * audio stays on the phone. Without one, the system recognizer is used with EXTRA_PREFER_OFFLINE;
     * that is a hint, and the system may still use the network.
     */
    private static boolean ensureRecognizer(Activity activity) {
        boolean wantOnDevice = Build.VERSION.SDK_INT >= 31
                && SpeechRecognizer.isOnDeviceRecognitionAvailable(activity);

        if (recognizer != null && recognizerIsOnDevice == wantOnDevice) {
            return true;
        }
        if (recognizer != null) {
            recognizer.destroy();
            recognizer = null;
        }

        if (wantOnDevice) {
            recognizer = SpeechRecognizer.createOnDeviceSpeechRecognizer(activity);
        } else if (SpeechRecognizer.isRecognitionAvailable(activity)) {
            recognizer = SpeechRecognizer.createSpeechRecognizer(activity);
        }
        recognizerIsOnDevice = wantOnDevice;

        if (recognizer == null) {
            return false;
        }
        recognizer.setRecognitionListener(new Listener());
        return true;
    }

    private static void listen(int expectedSession) {
        if (!active || expectedSession != session || recognizer == null) {
            return;
        }

        Intent intent = new Intent(RecognizerIntent.ACTION_RECOGNIZE_SPEECH);
        intent.putExtra(RecognizerIntent.EXTRA_LANGUAGE_MODEL, RecognizerIntent.LANGUAGE_MODEL_FREE_FORM);
        intent.putExtra(RecognizerIntent.EXTRA_LANGUAGE, languageTag);
        intent.putExtra(RecognizerIntent.EXTRA_PARTIAL_RESULTS, true);
        intent.putExtra(RecognizerIntent.EXTRA_MAX_RESULTS, 1);
        if (!allowCloud) {
            intent.putExtra(RecognizerIntent.EXTRA_PREFER_OFFLINE, true);
        }

        try {
            recognizer.startListening(intent);
        } catch (RuntimeException e) {
            finishWithError("other", "Could not start speech recognition: " + e.getMessage());
        }
    }

    private static void restartLater() {
        final int expected = session;
        MAIN.postDelayed(new Runnable() {
            @Override
            public void run() {
                listen(expected);
            }
        }, RESTART_DELAY_MS);
    }

    private static void finishWithError(String code, String message) {
        send("OnNativeSpeechError", code + "|" + message);
        finish();
    }

    private static void finish() {
        if (!active) {
            return;
        }
        active = false;
        session++;
        send("OnNativeListeningEnded", "");
    }

    private static void send(String method, String message) {
        if (unityObject != null) {
            UnityPlayer.UnitySendMessage(unityObject, method, message);
        }
    }

    private static final class Listener implements RecognitionListener {

        @Override
        public void onResults(Bundle results) {
            if (!active) {
                return;
            }

            ArrayList<String> texts = results.getStringArrayList(SpeechRecognizer.RESULTS_RECOGNITION);
            float[] scores = results.getFloatArray(SpeechRecognizer.CONFIDENCE_SCORES);

            if (texts != null && !texts.isEmpty()) {
                float confidence = scores != null && scores.length > 0 ? scores[0] : -1f;
                send("OnNativeSpeechResult", String.format(Locale.ROOT, "%.3f", confidence) + "|" + texts.get(0));
            }

            if (continuous) {
                restartLater();
            } else {
                finish();
            }
        }

        @Override
        public void onPartialResults(Bundle partialResults) {
            if (!active) {
                return;
            }
            ArrayList<String> texts = partialResults.getStringArrayList(SpeechRecognizer.RESULTS_RECOGNITION);
            if (texts != null && !texts.isEmpty() && texts.get(0).length() > 0) {
                send("OnNativePartialResult", texts.get(0));
            }
        }

        @Override
        public void onError(int error) {
            if (!active) {
                // ERROR_CLIENT and friends arrive after cancel(); the caller already stopped.
                return;
            }

            switch (error) {
                case SpeechRecognizer.ERROR_NO_MATCH:
                case SpeechRecognizer.ERROR_SPEECH_TIMEOUT:
                    if (continuous) {
                        restartLater();
                    } else {
                        finishWithError("no_speech", "No speech was recognized");
                    }
                    break;

                case SpeechRecognizer.ERROR_RECOGNIZER_BUSY:
                    if (recognizer != null) {
                        recognizer.cancel();
                    }
                    restartLater();
                    break;

                case SpeechRecognizer.ERROR_INSUFFICIENT_PERMISSIONS:
                    finishWithError("permission", "Microphone permission is required");
                    break;

                case SpeechRecognizer.ERROR_NETWORK:
                case SpeechRecognizer.ERROR_NETWORK_TIMEOUT:
                case SpeechRecognizer.ERROR_SERVER:
                    finishWithError("network", "Speech recognition needs a network connection on this device");
                    break;

                case SpeechRecognizer.ERROR_LANGUAGE_NOT_SUPPORTED:
                case SpeechRecognizer.ERROR_LANGUAGE_UNAVAILABLE:
                    finishWithError("unavailable", "Speech recognition does not support " + languageTag + " on this device");
                    break;

                default:
                    finishWithError("other", "Speech recognition error " + error);
                    break;
            }
        }

        @Override
        public void onReadyForSpeech(Bundle params) {
        }

        @Override
        public void onBeginningOfSpeech() {
        }

        @Override
        public void onRmsChanged(float rmsdB) {
        }

        @Override
        public void onBufferReceived(byte[] buffer) {
        }

        @Override
        public void onEndOfSpeech() {
        }

        @Override
        public void onEvent(int eventType, Bundle params) {
        }
    }
}
