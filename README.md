# SFTP Multi-Auth File Transfer — Clean README

A compact, practical README showing how to configure an OpenSSH SFTP server on **Windows**, set up multi-mode authentication (password, key, passphrase-protected key, and certificate), and transfer files reliably. This single-file README focuses on clarity and correctness.

---

## Table of Contents

1. Overview
2. Prerequisites
3. Server setup (step-by-step)
4. Generating keys & certificates (examples)
5. Client examples (SFTP commands)
6. Example transfer workflow (what the script does)
7. Permissions & common issues
8. FAQ
9. Troubleshooting checklist

---

## 1. Overview

This README describes a minimal, repeatable process to:

* Create a Windows SFTP user for OpenSSH
* Configure `sshd_config` for an `internal-sftp` chrooted user
* Install/authorize public keys, including a CA-signed user certificate
* Demonstrate client usages for: password, private key (with/without passphrase), and certificate-based authentication
* Show a robust transfer flow that records metadata about each transfer

This file is written to be copy-paste ready into `README.md` in your repo.

---

## 2. Prerequisites

* Windows Server / Windows 10+ with **OpenSSH Server** installed and running.
* Administrative privileges for user & permission changes.
* OpenSSH client (`ssh`, `sftp`, `ssh-keygen`) available on client machine.

---

## 3. Server setup (step-by-step)

**Create a local SFTP user**

```powershell
# Create user (change password to a secure value)
net user sftpuser "Pass@123" /add
```

**Create .ssh directory and authorized_keys**

```powershell
mkdir C:\Users\sftpuser\.ssh
notepad C:\Users\sftpuser\.ssh\authorized_keys
# paste public key content into authorized_keys
```

**Fix permissions (OpenSSH on Windows is strict)**

```powershell
# Remove inheritance and grant explicit permissions
icacls "C:\Users\sftpuser\.ssh" /inheritance:r
icacls "C:\Users\sftpuser\.ssh" /grant "SYSTEM:(F)" "Administrators:(F)" "sftpuser:(F)"
icacls "C:\Users\sftpuser\.ssh\authorized_keys" /inheritance:r
icacls "C:\Users\sftpuser\.ssh\authorized_keys" /grant "SYSTEM:(F)" "Administrators:(F)" "sftpuser:(F)"
```

**Configure sshd to force internal-sftp for this user**

Edit `C:\ProgramData\ssh\sshd_config` and append (or modify) a `Match` block:

```
Match User sftpuser
    ForceCommand internal-sftp
    AuthorizedKeysFile C:/Users/sftpuser/.ssh/authorized_keys
    ChrootDirectory C:/sftp-root/%u    # optional: use chroot if desired; ensure permissions
```

**Restart SSHD service**

```powershell
Restart-Service sshd
```

> Note: The SSH service listens on the port defined in `sshd_config` (default `22`). You may change it — SFTP will use whatever SSH port the server is configured to listen on.

---

## 4. Generating keys & certificates (examples)

**Generate an ed25519 keypair (no passphrase)**

```powershell
ssh-keygen -t ed25519 -f C:\Users\DevUser\.ssh\id_ed25519 -C "regular_key"
```

**Create a copy and add a passphrase**

```powershell
Copy-Item "C:\Users\DevUser\.ssh\id_ed25519" "C:\Users\DevUser\.ssh\id_ed25519_encrypt"
ssh-keygen -p -f "C:\Users\DevUser\.ssh\id_ed25519_encrypt"
# You will be prompted to enter a new passphrase
```

**Create a CA and sign a user key (certificate-based auth)**

```powershell
# generate CA keypair
ssh-keygen -t ed25519 -f C:\Users\DevUser\.ssh\ssh_ca -C "SFTP_CA"
# sign user public key to produce user certificate
ssh-keygen -s C:\Users\DevUser\.ssh\ssh_ca -I user_cert -n sftpuser -V +52w C:\Users\DevUser\.ssh\id_ed25519.pub
# copy CA public key to server and reference it from sshd_config (TrustedUserCAKeys)
Copy-Item C:\Users\DevUser\.ssh\ssh_ca.pub C:\ProgramData\ssh\ssh_ca.pub
```

Add or set in server `sshd_config`:

```
TrustedUserCAKeys C:/ProgramData/ssh/ssh_ca.pub
```

Restart `sshd` after changes.

---

## 5. Client examples (SFTP)

**Username + password**

```bash
sftp sftpuser@your.server.example.com
# or to force password auth from a client that would try pubkey first:
sftp -o PubkeyAuthentication=no sftpuser@your.server.example.com
```

**Username + private key (no passphrase)**

```bash
sftp -i "C:/Users/DevUser/.ssh/id_ed25519" -o PasswordAuthentication=no sftpuser@your.server.example.com
```

**Passphrase-protected key**

```bash
sftp -i "C:/Users/DevUser/.ssh/id_ed25519_encrypt" -o PasswordAuthentication=no sftpuser@your.server.example.com
# client will prompt for passphrase
```

**Certificate-based auth (user certificate + private key)**

```bash
sftp -i "C:/Users/DevUser/.ssh/id_ed25519" -o CertificateFile="C:/Users/DevUser/.ssh/id_ed25519-cert.pub" -o PasswordAuthentication=no sftpuser@your.server.example.com
```

---

## 6. Example transfer workflow (scripted behavior)

A helper script or program should perform these steps:

1. Read the record ID and source path (e.g. `D:\New\New Text Document.txt`).
2. Build SFTP client using selected auth method.
3. Connect to server and create a timestamped destination folder, e.g. `C:\Newfolder\UID{id}\(yyyy-MM-dd_HH-mm-ss)`.
4. Upload the file to the timestamped directory.
5. Create a `TransferInfo.txt` file with metadata:

   * Timestamp
   * Record ID
   * Source path
   * Target path on server
   * Authentication method used
   * Host, port, username
6. Disconnect and return a JSON object with saved local/remote paths.

## 7. Permissions & common issues

* If OpenSSH refuses to use `authorized_keys`, **verify Windows ACLs** — incorrect inheritance or lacking SYSTEM/Administrators access will break key auth.
* Use `icacls` to confirm permissions; OpenSSH typically requires that the `.ssh` folder and `authorized_keys` are not group/world writable and inheritance is removed.
* If multiple keys exist in different locations, ensure `AuthorizedKeysFile` in `sshd_config` points to the file you manage.
* Check `C:\ProgramData\ssh\logs\sshd.log` (or Windows Event Log) for detailed messages.

---

## 8. FAQ

**Q: Is port 22 mandatory?**
A: No — `22` is the default SSH/SFTP port, but you can configure `Port <n>` in `sshd_config` to use a different port. Clients must connect to the same port.

**Q: Is username mandatory?**
A: Yes — SSH/SFTP requires a username to map to a server account or identity.

**Q: Can I combine auth methods?**
A: You can require multiple factors on the server (e.g., `AuthenticationMethods publickey,password`) but doing so requires careful server config. Test thoroughly.

---

## 9. Troubleshooting checklist

* [ ] Confirm `sshd` is running and listening (`netstat -an` or `Get-NetTCPConnection`).
* [ ] Confirm correct port in `sshd_config` and firewall rules open the port.
* [ ] Verify `authorized_keys` contents and exact path configured.
* [ ] Verify ACLs with `icacls` and remove inheritance if needed.
* [ ] If using CA-signed certs, ensure `TrustedUserCAKeys` points to the CA public key and `sshd` was restarted.
* [ ] Inspect `sshd` logs for errors and address the first reported error.

---

## License

MIT

---

If you want, I can also produce a ready-to-run PowerShell script that performs the server-side setup (user creation, permission fixing, and sshd_config patch). Let me know and I will add it.
