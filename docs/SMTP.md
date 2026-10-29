# Verification email

`docker compose up --build` starts Mailpit alongside the APIs. No email account or SMTP credentials are needed. Open [the local inbox](http://localhost:8025), find the message addressed to your registration email, and submit its token to `POST /api/v1/email-verifications` in Users Swagger. The message includes the request body, UTC expiry and single-use instructions. It does not contain a verification link because verification is a POST API operation.

Mailpit captures SMTP messages locally; it does **not** deliver them to Gmail, Outlook or other internet inboxes. Its database uses the `mailpit-data` volume, so messages survive ordinary container restarts. Retention is bounded by Mailpit's default message limit. Both the inbox and SMTP ports bind only to loopback on the host. Anyone with access to the local inbox can see its verification tokens; use test addresses and no real SMTP credentials for this setup.

## Local debugging

Start the inbox with `docker compose up -d mailpit`, then run Users in Visual Studio or PowerShell as usual. Development configuration connects to `localhost:1025`; Compose overrides the host to `mailpit`. Override `MAILPIT_UI_PORT` / `MAILPIT_SMTP_PORT` for host port conflicts; also update `Email__Smtp__Port` when running Users outside Docker with a different SMTP host port. Container-to-container SMTP still uses port 1025.

`GET /dev/mailbox` remains a key-protected, in-memory preview for the existing reviewer script and API explorer. With SMTP selected, it contains only messages accepted by SMTP during that Users process. It is not a second delivery channel and is cleared on Users restart; Mailpit retains its own inbox. To explicitly use the previous in-memory-only sender, set `Email__Provider=Mailbox` in Development.

## Failure and retry

Registration commits the user, hashed verification token and wallet-provisioning outbox record before attempting email. SMTP has a five-second total timeout by default. A failed or uncertain send returns registration `201` with `verificationDeliveryStatus=unavailable`; it never removes the registered user or outbox. `sent` means SMTP acceptance, not guaranteed delivery to an internet mailbox.

Recover through `POST /api/v1/email-verifications/resend` with the registered email/password. It replaces the token, invalidating older messages. Successful resend returns 204; another send failure returns retryable 503. SMTP acknowledgement loss can leave an email in the inbox even when the caller sees unavailable. Resend then rotates that token. Verification still expires after 24 hours, consumes atomically once, and stores only the token hash in Users PostgreSQL. There is no automatic email retry worker or durable plaintext email queue.

## Other SMTP servers

The Infrastructure sender uses MailKit. These environment settings configure an external SMTP service when one is explicitly chosen:

| Setting | Meaning |
| --- | --- |
| `Email__Provider` | `Smtp`; outside Development, the unconfigured default remains `Unavailable` |
| `Email__Smtp__Host` / `Port` | Your SMTP server and port |
| `Email__Smtp__FromAddress` / `FromName` | Authorized sender address and display name |
| `Email__Smtp__Security` | `StartTls` (required upgrade) or `SslOnConnect`; `None` is accepted only in Development for local Mailpit |
| `Email__Smtp__Username` / `Password` | Optional credentials; supply both together through local secrets/environment, never source control |
| `Email__Smtp__TimeoutSeconds` | Total delivery bound, 1–30 seconds; default 5 |

Certificate validation remains enabled. External SMTP credentials, sender authorization and actual internet delivery have not been tested. Use the SMTP provider's instructions when choosing TLS mode and port; no external provider is required for the assessment.

See the [MailKit API](https://mimekit.net/docs/html/M_MailKit_Net_Smtp_SmtpClient_ConnectAsync_2.htm) and [Mailpit runtime options](https://mailpit.axllent.org/docs/configuration/runtime-options/) for transport configuration details.

## Tests

The existing command now also runs actual Mailpit/PostgreSQL verification tests:

```sh
docker compose -f compose.yaml -f compose.tests.yaml run --build --rm tests
```

Those tests read uniquely addressed test messages without clearing the inbox. Additional loopback SMTP tests cover message content, server rejection, bounded timeout, cancellation, preview behavior and lost QUIT acknowledgement. Host-side integration runs require `TestPostgres`, `TestMailpitHttpUrl`, and optionally `TestMailpitSmtpHost` / `TestMailpitSmtpPort`.
