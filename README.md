# SFTP Multi-Auth File Transfer

A comprehensive example of SFTP file transfer using multiple authentication methods with OpenSSH on Windows. This project demonstrates how to configure an SFTP server, set up users and keys, and transfer files securely using different authentication types.

---

## Table of Contents
1. [SFTP Server Configuration](#sftp-server-configuration)  
2. [Authentication Methods](#authentication-methods)  
3. [Code Functionality](#code-functionality)  
4. [Common Issues & Blockers](#common-issues--blockers)  
5. [Q & A](#q--as)  

---

## SFTP Server Configuration

### 1. Create SFTP User
```powershell
net user sftpuser Pass@123 /add
2. Create .ssh Folder
mkdir C:\Users\sftpuser\.ssh

3. Create authorized_keys

Copy your public key into this file:

notepad C:\Users\sftpuser\.ssh\authorized_keys

4. Fix Permissions (Critical)
icacls "C:\Users\sftpuser\.ssh" /inheritance:r
icacls "C:\Users\sftpuser\.ssh" /grant "SYSTEM:(F)"
icacls "C:\Users\sftpuser\.ssh" /grant "Administrators:(F)"
icacls "C:\Users\sftpuser\.ssh" /grant "sftpuser:(F)"

icacls "C:\Users\sftpuser\.ssh\authorized_keys" /inheritance:r
icacls "C:\Users\sftpuser\.ssh\authorized_keys" /grant "SYSTEM:(F)"
icacls "C:\Users\sftpuser\.ssh\authorized_keys" /grant "Administrators:(F)"
icacls "C:\Users\sftpuser\.ssh\authorized_keys" /grant "sftpuser:(F)"

5. Configure SSHD

Edit sshd_config:

notepad "C:\ProgramData\ssh\sshd_config"


Add:

Match User sftpuser
    ForceCommand internal-sftp
    AuthorizedKeysFile C:/Users/sftpuser/.ssh/authorized_keys

6. Restart SSH service
Restart-Service sshd

7. Generate Keys
ssh-keygen -t ed25519 -f "C:\Users\XD24100BT\.ssh\id_ed25519" -C "regular_key"

8. Create Passphrase-Protected Key
Copy-Item "C:\Users\XD24100BT\.ssh\id_ed25519" "C:\Users\XD24100BT\.ssh\id_ed25519_encrypt"
ssh-keygen -p -f "C:\Users\XD24100BT\.ssh\id_ed25519_encrypt"
# Enter passphrase (example): Abc@123

9. Create Certificate Authority + User Certificate
ssh-keygen -t ed25519 -f "C:\Users\XD24100BT\.ssh\ssh_ca" -C "SFTP_CA"
ssh-keygen -s "C:\Users\XD24100BT\.ssh\ssh_ca" -I user_cert -n sftpuser -V +52w "C:\Users\XD24100BT\.ssh\id_ed25519.pub"

10. Copy CA Public Key to SSH Server
Copy-Item "C:\Users\XD24100BT\.ssh\ssh_ca.pub" "C:\ProgramData\ssh\ssh_ca.pub"

11. Verification

Check logs for key issues:

Select-String -Path "C:\ProgramData\ssh\logs\sshd.log" -Pattern "authorized_keys"

Authentication Methods
1. Username + Password
sftp sftpuser@localhost
# or block publickey
sftp -o PubkeyAuthentication=no sftpuser@localhost

2. Username + Private Key (No Passphrase)
sftp -i "C:\Users\XD24100BT\.ssh\id_ed25519" -o PubkeyAuthentication=yes -o PasswordAuthentication=no sftpuser@localhost

3. Username + Passphrase-Protected Key
sftp -i "C:\Users\XD24100BT\.ssh\id_ed25519_encrypt" -o PubkeyAuthentication=yes -o PasswordAuthentication=no sftpuser@localhost
# Enter passphrase: Abc@123

4. Certificate-Based Authentication
sftp -i "C:\Users\XD24100BT\.ssh\id_ed25519" `
     -o CertificateFile="C:\Users\XD24100BT\.ssh\id_ed25519-cert.pub" `
     -o PasswordAuthentication=no `
     sftpuser@localhost

Code Functionality

Fetch record from database using ID

Build SFTP client based on authentication method:

Password

Private Key

Private Key + Passphrase

Certificate + Private Key

Connect to the SFTP server

Copy local file:

D:\New\New Text Document.txt


to a time-stamped directory:

C:\Newfolder\UID{id}(yyyy-MM-dd_HH-mm-ss)


Generate TransferInfo.txt with:

Timestamp

Record ID

Source file path

Target file path

Authentication method

Host, Port, Username

Disconnect from SFTP

Return JSON response with saved paths

Common Issues & Blockers

SYSTEM could not read authorized_keys (Permission denied)

Incorrect inheritance on .ssh folder

Multiple key locations caused confusion

Always verify icacls output – OpenSSH on Windows is strict

Q & A

Is port 22 mandatory?
Yes, SFTP only works if the server is listening on the correct port.

Is username mandatory for all authentication types?
Yes, username is always required for SFTP.

What if a different port is used?
Update sshd_config with the new port and restart SSH service.