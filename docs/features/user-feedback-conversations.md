# User Feedback Conversations

## Purpose

BadWolfQuiz provides a persistent feedback channel between site users and the
developer. It is intended for questions, ideas, problem reports, and general
feedback rather than only one-time questions.

A submission creates a conversation that can contain multiple user and developer
messages. Users can return to conversations from the same browser, while the
developer can manage them from the application and receive actionable Discord
notifications.

## Conversation model

`UserQuestion` is the conversation root. Individual entries are stored as
`UserQuestionMessage` records rather than overwriting a single question/answer
pair.

A conversation has a public token used by user-facing routes. The token is the
external identifier and must be treated as an unguessable capability; internal
database identifiers are not exposed as the normal user access mechanism.

Messages preserve their author side and creation order so the complete thread can
be rendered chronologically.

Discord notification identifiers belong to the individual user message that
created the notification. This allows Discord cleanup to target the corresponding
notification instead of treating Discord state as a single property of the whole
conversation.

## User workflow

The contact page explains that users may:

- ask the developer a question;
- suggest an idea;
- report a problem;
- leave other feedback.

After submitting a message, the user is taken to the conversation view. The same
conversation can later receive additional messages from both sides.

The user-facing terminology presents these records as messages/conversations
rather than implying that every contact must be a literal question.

## Local conversation history

The site does not require an authenticated account merely to return to previously
submitted conversations. Public conversation tokens are stored locally in the
browser in the `BadWolfQuiz.UserQuestions` cookie.

`UserQuestionHistoryService` owns this browser-history behavior.

The **My messages** page:

- resolves the locally stored conversation tokens;
- lists conversations that still exist;
- lets the user reopen a conversation;
- lets the user delete a conversation.

The navigation entry is shown when locally saved conversation history is
available.

Browser history is a convenience mechanism, not the canonical conversation
store. Clearing browser data may remove the local list without deleting the
server-side conversation.

## Developer inbox

The developer has a dedicated inbox for user conversations.

The inbox and individual conversation view distinguish user and developer
messages visually and keep message content, metadata, status, reply controls, and
destructive actions separate.

The developer can open a conversation, review the complete chronological thread,
reply, and delete the conversation.

## Discord notifications

New user messages are announced through the configured Discord bot rather than a
question-specific webhook.

The notification is sent to the configured Discord guild and channel and is tied
to the corresponding `UserQuestionMessage` through its Discord message ID.

Discord provides actions for working with the conversation, including opening or
replying to the relevant conversation and deleting it where that action is
available. Destructive actions require confirmation.

The obsolete `Discord:QuestionWebhookUrl` configuration is not part of the
current design.

## Deletion

Conversation deletion is centralized so every entry point has the same
semantics. A conversation may be deleted from the supported user, developer, or
Discord workflows.

Deleting a conversation removes the complete conversation and its messages. The
deletion workflow also attempts to clean up Discord notifications associated with
the conversation's user messages.

A stale token in local browser history must not recreate or expose a deleted
conversation.

## Presentation

Conversation pages use message cards and distinct visual accents so adjacent
messages and replies are easy to distinguish. The presentation supports both
light and dark themes and responsive layouts.

Preview text on the local-history page is normalized for compact display rather
than preserving formatting whitespace that would create misleading visual gaps.

## Privacy

The local conversation-history cookie contains conversation identifiers needed to
restore the user's local list. It is documented on the site's Privacy & Cookies
page together with authentication/security cookies, language preference, and
other browser data used by the application.

Embedded YouTube content elsewhere in the site uses privacy-enhanced
`youtube-nocookie.com` embeds. Hosts may continue entering normal supported
YouTube URLs; the application performs the embed conversion.

## Navigation

Feedback-related navigation is grouped together:

- **Contact the developer**;
- **My messages** when local history exists;
- **User messages** for the developer;
- **FAQ**.

The wider navigation groups quiz participation, game activity, communication,
account settings, and authentication actions separately.

## Testing expectations

Regression coverage should verify at minimum that:

- a new submission creates a conversation and its first message;
- additional user and developer messages remain in chronological order;
- public-token access resolves the intended conversation;
- locally saved conversation tokens populate the history page;
- missing or deleted conversations do not break local history;
- a user can reopen and delete a saved conversation;
- the developer can review, reply to, and delete conversations;
- a new user message creates the expected Discord bot notification;
- Discord message IDs are associated with the correct user messages;
- deletion performs the expected Discord cleanup;
- the obsolete question webhook is not required;
- conversation UI remains readable in light and dark themes and on narrow
  screens.
