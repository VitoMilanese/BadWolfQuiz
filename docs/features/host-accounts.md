# Host Accounts and Data Ownership

## Authentication

Hosts create their own account with an email address and password. Authentication
uses an encrypted application cookie. Passwords are stored only through ASP.NET
Core's password hasher.

Authenticated hosts can change their password after confirming the current one.
Forgotten passwords use a cryptographically random, single-use token that expires
after one hour. Only the token hash is stored. Resend delivers the reset link using
the `Resend:ApiToken` and `Resend:From` values from application configuration.

All Razor Pages below `/Admin` require an authenticated host. Player join and
gameplay pages remain anonymous.

## Ownership

Every newly created or imported quiz belongs exclusively to the host who created
or imported it. A completed game belongs to the host who launched it. There is
currently no global administrator role, quiz sharing, or ownership transfer.

Ownership is enforced by database query filters across quizzes, their child
entities, completed games, players, questions, buzzes, and answer results. Active
in-memory games also carry the host identifier and reject access from a different
authenticated host.

## Existing installations

The ownership columns are nullable only to support databases created before host
accounts existed. During the first successful registration, all quizzes and game
history without an owner are assigned to that first host in the same database
transaction. Later registrations never claim existing data.

## Next step

Player QR joining will encode the public `/Join/{code}` URL and does not require
a player account.
