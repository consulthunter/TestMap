# Single Repository Target Fixtures

These fixtures model URL parsing, provider observations, resolution records, and schema-3 target
manifests without network access. Repository names and commits are synthetic unless a file explicitly
points to the public `powershell/platyps` documentation example.

Credential-shaped strings are negative-test sentinels only. Use conspicuous values such as
`ghp_SYNTHETIC_NOT_A_SECRET`, never a copied token, provider response body, authorization header, or
stack trace. Tests must assert that sentinels do not survive into published records or console text.

Times are fixed UTC values, commits are full nonzero 40-character hexadecimal strings, and each
fixture documents whether a second complete resolution pass is expected.
