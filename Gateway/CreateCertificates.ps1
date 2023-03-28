# Скрипт создания сертификатов клиента для HTTPS и электронной подписи.

$path = "C:\Dev\Emerald.Examples\Integrator"
$password = ConvertTo-SecureString 'password' -AsPlainText -Force

# Корневой сертфикат HTTPS клиента.

$certServerRoot = New-SelfSignedCertificate -Type Custom -KeySpec Signature -Subject "CN=RootEmeraldExamplesIntegratorHttpsOrganization" -KeyExportPolicy Exportable -HashAlgorithm sha256 -KeyLength 2048 -CertStoreLocation "Cert:\CurrentUser\My" -KeyUsageProperty Sign -KeyUsage CertSign -NotAfter (Get-Date).AddYears(20)
$certServerRoot | Export-PfxCertificate -FilePath "$path\root.emerald.examples.integrator.https.organization.pfx" -Password $password
$certServerRoot | Export-Certificate -Type cer -FilePath "$path\root.emerald.examples.integrator.https.organization.cer" -Force 

# Сертфикат HTTPS клиента, подписанный корневым сертфикатом HTTPS клиента.
#
# Сервер требует - RSA с длинной ключа не менее 2048 бит.
#

$certServer = New-SelfSignedCertificate -Type Custom -KeySpec Signature -Subject "CN=EmeraldExamplesIntegratorHttpsOrganization" -KeyExportPolicy Exportable -HashAlgorithm sha256 -KeyLength 2048 -CertStoreLocation "Cert:\CurrentUser\My" -KeyUsageProperty Sign -KeyUsage DigitalSignature, KeyEncipherment, DataEncipherment -Signer $certServerRoot -NotAfter (Get-Date).AddYears(20)
$certServer | Export-PfxCertificate -FilePath "$path\emerald.examples.integrator.https.organization.pfx" -Password $password
$certServer | Export-Certificate -Type cer -FilePath "$path\emerald.examples.integrator.https.organization.cer" -Force 

# Удаление сертифкатов из хранилища

Get-ChildItem Cert:\CurrentUser\My | Where-Object {$_.Thumbprint -match $certServerRoot.Thumbprint} | Remove-Item 
Get-ChildItem Cert:\CurrentUser\My | Where-Object {$_.Thumbprint -match $certServer.Thumbprint} | Remove-Item 
Get-ChildItem Cert:\CurrentUser\CA | Where-Object {$_.Thumbprint -match $certServerRoot.Thumbprint} | Remove-Item 
Get-ChildItem Cert:\CurrentUser\CA | Where-Object {$_.Thumbprint -match $certServer.Thumbprint} | Remove-Item 

# Корневой сертфикат электронной подписи клиента.

$certServerRoot = New-SelfSignedCertificate -Type Custom -KeySpec Signature -Subject "CN=RootEmeraldExamplesIntegratorSignatureOrganization" -KeyExportPolicy Exportable -HashAlgorithm sha256 -KeyLength 2048 -CertStoreLocation "Cert:\CurrentUser\My" -KeyUsageProperty Sign -KeyUsage CertSign -NotAfter (Get-Date).AddYears(20)
$certServerRoot | Export-PfxCertificate -FilePath "$path\root.emerald.examples.integrator.signature.organization.pfx" -Password $password
$certServerRoot | Export-Certificate -Type cer -FilePath "$path\root.emerald.examples.integrator.signature.organization.cer" -Force 

# Сертфикат электронной подписи клиента, подписанный корневым сертфикатом электронной подписи клиента.
#
# Сервер требует - RSA с длинной ключа не менее 2048 бит и алгоритмом хэш-функции SHA-256.
#

$certServer = New-SelfSignedCertificate -Type Custom -KeySpec Signature -Subject "CN=EmeraldExamplesIntegratorSignatureOrganization" -KeyExportPolicy Exportable -HashAlgorithm sha256 -KeyLength 2048 -CertStoreLocation "Cert:\CurrentUser\My" -KeyUsageProperty Sign -KeyUsage DigitalSignature -Signer $certServerRoot -NotAfter (Get-Date).AddYears(20)
$certServer | Export-PfxCertificate -FilePath "$path\emerald.examples.integrator.signature.organization.pfx" -Password $password
$certServer | Export-Certificate -Type cer -FilePath "$path\emerald.examples.integrator.signature.organization.cer" -Force 

# Удаление сертифкатов из хранилища

Get-ChildItem Cert:\CurrentUser\My | Where-Object {$_.Thumbprint -match $certServerRoot.Thumbprint} | Remove-Item 
Get-ChildItem Cert:\CurrentUser\My | Where-Object {$_.Thumbprint -match $certServer.Thumbprint} | Remove-Item 
Get-ChildItem Cert:\CurrentUser\CA | Where-Object {$_.Thumbprint -match $certServerRoot.Thumbprint} | Remove-Item 
Get-ChildItem Cert:\CurrentUser\CA | Where-Object {$_.Thumbprint -match $certServer.Thumbprint} | Remove-Item 
