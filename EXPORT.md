# Export Compliance Notice

## Classification

This software is classified under Export Control Classification Number (ECCN) **5D002** - Information Security Software.

## License Exception

NASA-access-LAUNCHPAD is exported under **License Exception TSU** (Technology and Software Unrestricted) per [15 CFR § 740.13(e)](https://www.law.cornell.edu/cfr/text/15/740.13) of the Export Administration Regulations (EAR).

This exception applies because:

1. **Publicly Available**: The source code is publicly available on GitHub
2. **Open Source**: Released under MIT License with no payment required for commercial use
3. **No Custom Cryptography**: All cryptographic operations are performed by the host operating system and Microsoft WebView2 runtime; this software contains no original cryptographic implementations.

## Cryptographic Functionality

NASA-access-LAUNCHPAD does not perform any cryptographic operations directly. It hosts a Microsoft WebView2 control which performs standard TLS client certificate authentication via the Windows Certificate Store. The application itself only handles window management and UI focus assistance for the Windows Security PIN dialog.

Cryptographic operations involved (all performed by Windows / WebView2, not by this software):
- TLS handshake and session establishment
- X.509 client certificate selection and signing for client authentication
- CAC/PIV smart card PIN verification

## BIS Notification

Per 15 CFR § 740.13(e)(3), notification of publicly available encryption source code has been submitted to:

- Bureau of Industry and Security (BIS): crypt@bis.doc.gov
- ENC Encryption Request Coordinator: enc@nsa.gov

Source code location: https://github.com/brucedombrowski/NASA-access-LAUNCHPAD

Notification submitted: 2026-05-07

## Restrictions

This software may not be exported or re-exported to:

- Countries under U.S. embargo (currently: Cuba, Iran, North Korea, Syria, and the Crimea, Donetsk, and Luhansk regions of Ukraine)
- Denied persons or entities on the BIS Entity List
- End-users involved in weapons of mass destruction proliferation

## Disclaimer

This export compliance notice is provided for informational purposes. Users are responsible for ensuring their use complies with applicable export control laws.

## References

- [EAR Part 740 - License Exceptions](https://www.bis.gov/ear/title-15/subtitle-b/chapter-vii/subchapter-c/part-740)
- [15 CFR § 740.13 - TSU](https://www.law.cornell.edu/cfr/text/15/740.13)
- [BIS Encryption Policy Guidance](https://www.bis.doc.gov/index.php/policy-guidance/encryption)
