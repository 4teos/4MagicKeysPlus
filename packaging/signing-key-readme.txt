4MagicKeys+ signing key
=======================

This folder holds the key that signed the 4MagicKeys+ driver and agent on this computer.

  signing-key.pfx   certificate with its private key (no password)
  README.txt        this file


Two different things

1. The key in this folder (signing-key.pfx) is needed only to SIGN. sign.cmd uses it when you
   sign a release. The installed driver and agent do not need it, their signatures are already
   inside their files. You can delete this folder at any time, they keep working.

2. The certificate in the Windows certificate stores (Local Machine: "Trusted Root
   Certification Authorities" and "Trusted Publishers") is what makes Windows trust the signed
   driver and agent. It has to stay as long as they are installed: without it the agent does
   not start and Windows may refuse to load the driver.


What it is

sign.cmd from the release archive created a self-signed code-signing certificate,
"4MagicKeysPlus local signing" (valid until 2040), saved it here with its private key, put its
public part into the two stores above and signed the driver (.sys, .cat) and the agent (.exe).
This folder is readable only by Administrators and SYSTEM.


Why the key is kept

So that the next sign.cmd signs with the same certificate instead of creating one more and
trusting it as well. Nothing else depends on it.


What to do with it

Nothing, if you are fine with it lying here. Do not copy or share the .pfx: whoever has it can
sign programs that this computer trusts.


If you do not want to keep the key

Delete this folder. The installed driver and agent are not affected. The next sign.cmd creates
a new key and certificate and trusts them too (the old certificate stays in the stores).


If you want to remove the trust as well (for example when you uninstall 4MagicKeys+)

1. Uninstall the driver and the agent first. Windows stops trusting what was signed with the
   certificate as soon as it is gone from the stores.
2. Delete this folder. sign.cmd refuses to use a key whose certificate is not in the stores.
3. Remove the certificates from the two stores (PowerShell as administrator):

   Get-ChildItem Cert:\LocalMachine\Root, Cert:\LocalMachine\TrustedPublisher | Where-Object Subject -eq 'CN=4MagicKeysPlus local signing' | Remove-Item
