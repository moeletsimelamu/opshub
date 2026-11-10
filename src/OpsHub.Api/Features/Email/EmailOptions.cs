namespace OpsHub.Api.Features.Email;

/// <summary>
/// Bound from the "Email" configuration section. Email is off unless switched on: then OpsHub
/// sends password-reset links and ticket notifications.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public bool Enabled { get; set; }

    /// <summary>Sender address, e.g. "opshub@yourcompany.example".</summary>
    public string? From { get; set; }

    public string FromName { get; set; } = "OpsHub";

    /// <summary>
    /// The address people open OpsHub at, e.g. "https://opshub.yourcompany.example". Links in
    /// emails point here; it can't be taken from a request because emails are sent in the background.
    /// </summary>
    public string? PublicBaseUrl { get; set; }

    /// <summary>
    /// Development: write each email as an .eml file in this folder instead of sending it.
    /// When set, the SMTP settings are ignored.
    /// </summary>
    public string? PickupDirectory { get; set; }

    public SmtpOptions Smtp { get; set; } = new();

    public sealed class SmtpOptions
    {
        public string? Host { get; set; }

        public int Port { get; set; } = 587;

        /// <summary>Upgrade the connection with STARTTLS (port 587). Leave on for real mail servers.</summary>
        public bool UseTls { get; set; } = true;

        public string? Username { get; set; }

        /// <summary>A secret: pass it as the Email__Smtp__Password environment variable.</summary>
        public string? Password { get; set; }
    }
}
