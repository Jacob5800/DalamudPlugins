# Feedback email relay setup

The plugin's **?** tab has a feedback box. Plugin users type a note and click **Send feedback**; the plugin sends the note in the background. It does not open a browser or mail program. The destination address is stored in this relay's private Apps Script properties, not in the plugin UI or this repository.

## One-time setup by the repository owner

1. Open [Google Apps Script](https://script.google.com/) while signed in to the Google account that will receive feedback, then create a new project.
2. Replace the starter code in `Code.gs` with this folder's `Code.gs` and save it.
3. Open **Project Settings** → **Script Properties** and add `FEEDBACK_RECIPIENT` with the feedback recipient's email address as its value. Keep that value out of this public repository.
4. Choose **Deploy** → **New deployment** → **Web app**. Set **Execute as** to your account and **Who has access** to **Anyone**, then deploy and authorize the script to send email. This public endpoint only accepts feedback text, has a 25-message daily limit, and always sends to the recipient stored in Script Properties.
5. Copy the deployed Web app URL into `FeedbackClient.cs` before the next release. Plugin users will not have to open or enter this URL.

## What is public and what is limited

- The Web app URL ships in the open-source plugin, so treat it as public. Only the recipient address is private.
- The relay allows 25 notes per UTC day in total and rejects identical notes received the same day. The plugin also waits 60 seconds between sends. Anyone who finds the URL can still use up the daily cap, so a note may occasionally be refused until the next day. Watch the mailbox for spam.
- Each note is sent to Google Apps Script and emailed to the recipient with the plugin version and a UTC timestamp. Nothing else is attached.

After the endpoint is connected and the plugin is rebuilt, submit one clearly labeled test note from the plugin's **?** tab to verify delivery. The relay adds the plugin version to the email subject and does not attach character names, world names, market data, or logs.
