# Security Policy

## Overview

Mechanic Scope is designed with a **local-first, privacy-focused architecture**. The application does not collect, transmit, or store user data on external servers. All data remains on the user's device.

---

## Security Design Principles

### Local-Only Data Storage
- All user data is stored locally on the device
- No cloud synchronization or external data transmission
- No user accounts or authentication required
- No analytics, telemetry, or tracking

### Minimal Permissions
The app requests only essential permissions:

| Permission | Purpose | When Requested |
|------------|---------|----------------|
| Camera | AR functionality | On first use |
| Microphone | Voice commands (optional) | When enabling voice |
| Storage | Import engine models | When importing files |

### No Network Requirements
- The app functions fully offline
- No internet connection required for any feature
- No external API calls or data fetching

---

## Supported Versions

| Version | Supported |
|---------|-----------|
| 0.3.x | Yes |
| 0.2.x | Yes |
| 0.1.x | Security fixes only |
| < 0.1 | No |

---

## Reporting a Vulnerability

We take security seriously. If you discover a security vulnerability, please report it responsibly.

### How to Report

**Do NOT open a public GitHub issue for security vulnerabilities.**

Instead, please email security concerns to the maintainers:
- Create a private security advisory on GitHub: [New Security Advisory](https://github.com/kase1111-hash/Mechanic-Scope/security/advisories/new)
- Or email the repository owner directly (see GitHub profile)

### What to Include

Please include the following in your report:
- Description of the vulnerability
- Steps to reproduce
- Potential impact
- Suggested fix (if any)
- Your contact information for follow-up

### Response Timeline

| Action | Timeline |
|--------|----------|
| Initial acknowledgment | Within 48 hours |
| Preliminary assessment | Within 1 week |
| Fix development | Depends on severity |
| Security advisory | After fix is released |

### What to Expect

1. **Acknowledgment**: We'll confirm receipt of your report
2. **Assessment**: We'll evaluate the vulnerability and its impact
3. **Communication**: We'll keep you informed of our progress
4. **Credit**: With your permission, we'll credit you in the security advisory

---

## Security Considerations for Users

### Device Security
- Keep your device's operating system updated
- Use device encryption when available
- Be cautious when importing models from untrusted sources

### Model and Procedure Files
- Only import engine models and procedures from trusted sources
- Malformed JSON files cannot execute code but may crash the app
- GLB/FBX files from untrusted sources should be scanned for malware

### App Installation
- Install only from official sources (App Store, Google Play, or GitHub releases)
- Verify app signatures when side-loading
- Do not install modified APKs from third parties

---

## Security Considerations for Contributors

### Code Review
- All contributions undergo code review
- Security-sensitive changes require additional review
- No third-party code without license verification

### Dependency Management
- Dependencies are pinned to specific versions
- Regular dependency audits for known vulnerabilities
- Minimal use of third-party libraries

### Secure Coding Practices
- Input validation for all user-provided data
- No dynamic code execution
- Proper error handling without exposing internals
- No hardcoded secrets or credentials

---

## Known Security Considerations

### File Parsing
- JSON parsing uses Unity's built-in JsonUtility (safe)
- GLB/FBX parsing uses vetted third-party libraries
- Malformed files may cause crashes but not code execution

### SQLite Database
- Database files are stored in app-private directories
- No sensitive user data stored (only repair progress)
- Database is not encrypted (no sensitive data to protect)

### AR Camera Access
- Camera feed is processed locally only
- No images are stored or transmitted
- No facial recognition or biometric data collection

---

## Security Updates

Security updates are released as patch versions (e.g., 0.3.1) and include:
- Fix for the vulnerability
- Updated CHANGELOG with security note
- GitHub security advisory (for significant issues)

Users are encouraged to:
- Enable automatic updates on their devices
- Watch this repository for security advisories
- Update promptly when security releases are available

---

## Scope

This security policy covers:
- The Mechanic Scope mobile application
- Official releases from this repository
- Documentation and example procedures

This policy does NOT cover:
- Third-party engine models or procedures
- Fork repositories
- User-modified builds
- Third-party app stores or distribution channels

---

## Contact

For security-related inquiries:
- GitHub Security Advisories: [Create Advisory](https://github.com/kase1111-hash/Mechanic-Scope/security/advisories/new)
- General questions: Open a GitHub Issue (for non-sensitive matters)

---

*Last updated: January 2026*
