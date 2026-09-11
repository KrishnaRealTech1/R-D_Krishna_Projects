# RealTech iTOLL Automation v1.0 - FTPS Certificate Validation Update

## Problem addressed

Explicit FTPS could connect to servers whose TLS certificate is accepted by WinSCP only after an operator trust decision, but the application correctly rejected the same certificate when Windows reported errors such as:

- `RemoteCertificateNameMismatch`
- `RemoteCertificateChainErrors`

This caused transaction synchronization to fail at `stage=image upload`.

## Changes

- Keeps normal Windows TLS certificate validation as the default.
- Adds **FTPS Certificate SHA-256 Fingerprint** to Server Panel > Server > Image Upload.
  - If the configured SHA-256 fingerprint exactly matches the presented server certificate, FTPS may continue even when the certificate has a name or chain validation error.
  - This provides certificate pinning and is the recommended compatibility method for a known legacy/self-signed FTPS server.
- Adds **Allow Invalid FTPS Certificate (Unsafe)** as an explicit emergency fallback.
  - Default is `false`.
  - When enabled, certificate identity validation is bypassed for FTPS and should only be used temporarily.
- Improves certificate failure diagnostics. The server log now reports the presented SHA-256 fingerprint, certificate subject, issuer, and TLS policy errors so the operator can verify the certificate in WinSCP before pinning it.
- Certificate callback changes are scoped to one FTPS operation and restored immediately after the operation.

## Recommended configuration

For a server whose certificate cannot pass normal Windows validation:

1. Verify the FTPS certificate in WinSCP.
2. Copy its SHA-256 certificate fingerprint.
3. Paste it into **FTPS Certificate SHA-256 Fingerprint**.
4. Leave **Allow Invalid FTPS Certificate (Unsafe)** unchecked.

The best long-term fix remains installing a trusted server certificate whose DNS name matches the configured FTPS host and whose full certificate chain is correctly served.
