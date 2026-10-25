using OpsHub.Domain.Tickets;
using OpsHub.Domain.Users;

namespace OpsHub.Api.Data.Seeding;

/// <summary>
/// Fictional demo data for "Marula Logistics", an invented company. People, emails
/// and tickets are made up. The .example domain is reserved for documentation (RFC 2606),
/// so these addresses can never belong to anyone.
/// </summary>
internal static class DemoData
{
    public const string EmailDomain = "marula.example";

    public static readonly string[] Categories =
        ["Hardware", "Software", "Access & Accounts", "Network", "Facilities"];

    public static readonly DemoUser[] Users =
    [
        new("lerato", "Lerato Dlamini", AppRoles.Administrator),

        new("sipho", "Sipho Ndlovu", AppRoles.SupportAgent),
        new("ayesha", "Ayesha Patel", AppRoles.SupportAgent),
        new("johan", "Johan van Wyk", AppRoles.SupportAgent),
        new("kagiso", "Kagiso Molefe", AppRoles.SupportAgent, IsActive: false),

        new("thandi", "Thandi Mokoena", AppRoles.Requester),
        new("pieter", "Pieter Botha", AppRoles.Requester),
        new("naledi", "Naledi Khumalo", AppRoles.Requester),
        new("ravi", "Ravi Govender", AppRoles.Requester),
        new("emma", "Emma Clarke", AppRoles.Requester),
        new("busisiwe", "Busisiwe Zulu", AppRoles.Requester),
    ];

    public static readonly DemoTicket[] Tickets =
    [
        new("VPN disconnects every few minutes when working from home",
            "Since Tuesday the VPN drops roughly every five minutes. I have to reconnect each time, which interrupts uploads to the finance system.",
            "Network", TicketPriority.High, "thandi", TicketStatus.InProgress, "sipho", CreatedDaysAgo: 1.2,
            Comments:
            [
                new("thandi", "It happens on both my home Wi-Fi and my phone hotspot."),
                new("sipho", "Gateway logs show repeated re-key failures for this account. Watching after the profile update.", IsInternal: true),
                new("sipho", "I've pushed an updated VPN profile to your laptop. Please restart and let me know if it still drops."),
            ]),

        new("New starter laptop for dispatch team (starts Monday)",
            "We have a new dispatch coordinator starting on Monday. Please prepare a standard laptop with the route planning app and email.",
            "Hardware", TicketPriority.Medium, "ravi", TicketStatus.Open, null, CreatedDaysAgo: 0.4),

        new("Cannot open month-end reconciliation workbook (file locked)",
            "The reconciliation workbook on the finance share says it is locked for editing by another user, but nobody in the team has it open.",
            "Software", TicketPriority.High, "thandi", TicketStatus.WaitingForInformation, "ayesha", CreatedDaysAgo: 2.5,
            Comments:
            [
                new("ayesha", "The lock is held by a session on PC FIN-07. Could you check whether that PC is switched on and who normally uses it?"),
            ]),

        new("Warehouse handheld scanner #4 not syncing stock counts",
            "Scanner #4 has shown 'sync pending' since 06:00. We are doing the quarterly stock take today and need it working.",
            "Hardware", TicketPriority.Critical, "pieter", TicketStatus.InProgress, "johan", CreatedDaysAgo: 0.3,
            Comments:
            [
                new("johan", "Likely a faulty charging cradle. Swapping the unit with the spare from the IT store.", IsInternal: true),
                new("johan", "On my way to the warehouse with a replacement scanner."),
            ]),

        new("Access to HR shared drive for new payroll clerk",
            "Please give our new payroll clerk read and write access to the Payroll folder on the HR shared drive. Approved by the HR manager.",
            "Access & Accounts", TicketPriority.Medium, "naledi", TicketStatus.Resolved, "ayesha", CreatedDaysAgo: 6, HoursToResolve: 5,
            Comments:
            [
                new("ayesha", "Access granted with read/write to the Payroll folder. Please ask them to sign out and back in."),
                new("naledi", "Confirmed, it works now. Thank you!"),
            ]),

        new("Second-floor printer jams on double-sided printing",
            "The printer next to the kitchen on the second floor jams every time someone prints double-sided. Single-sided is fine.",
            "Hardware", TicketPriority.Low, "emma", TicketStatus.Open, null, CreatedDaysAgo: 3),

        new("Email signature shows old office address",
            "My email signature still shows the old Rivonia office address. Can it be updated to the new head office?",
            "Software", TicketPriority.Low, "emma", TicketStatus.Closed, "sipho", CreatedDaysAgo: 20, HoursToResolve: 26),

        new("Password reset loop on the timesheet portal",
            "After resetting my password the timesheet portal sends me straight back to the reset page.",
            "Access & Accounts", TicketPriority.High, "busisiwe", TicketStatus.Resolved, "sipho", CreatedDaysAgo: 4, HoursToResolve: 3,
            Comments:
            [
                new("sipho", "I cleared the stale session on the portal side. Please try signing in again."),
            ]),

        new("Wi-Fi drops in the boardroom during video calls",
            "Video calls in the main boardroom freeze or drop after about 20 minutes. It started after the office move.",
            "Network", TicketPriority.Medium, "busisiwe", TicketStatus.InProgress, "johan", CreatedDaysAgo: 5),

        new("Route planning app crashes when exporting to PDF",
            "Exporting a route sheet to PDF closes the app without an error. Exporting to Excel still works.",
            "Software", TicketPriority.Medium, "ravi", TicketStatus.Open, "ayesha", CreatedDaysAgo: 1),

        new("Monitor flickering at reception desk",
            "The reception monitor flickers constantly, which makes it hard to read visitor details.",
            "Hardware", TicketPriority.Low, "naledi", TicketStatus.Closed, "johan", CreatedDaysAgo: 28, HoursToResolve: 50),

        new("Supplier invoices mailbox not receiving email",
            "The shared supplier invoices mailbox has not received anything since yesterday morning. Suppliers say their emails were sent.",
            "Software", TicketPriority.Critical, "thandi", TicketStatus.Resolved, "sipho", CreatedDaysAgo: 9, HoursToResolve: 2,
            Comments:
            [
                new("sipho", "A mail flow rule was blocking one external sender domain. Rule corrected and the held messages released.", IsInternal: true),
                new("sipho", "Fixed. Invoices from the last two days have now been delivered to the mailbox."),
            ]),

        new("Remove system access for contractor whose contract has ended",
            "Our warehouse systems contractor finished on Friday. Please remove their access to all systems.",
            "Access & Accounts", TicketPriority.High, "naledi", TicketStatus.Closed, "ayesha", CreatedDaysAgo: 15, HoursToResolve: 4),

        new("Server room air-conditioning alarm triggered",
            "The temperature alarm in the server room went off at 14:10. The room feels warm.",
            "Facilities", TicketPriority.Critical, "pieter", TicketStatus.Resolved, "johan", CreatedDaysAgo: 11, HoursToResolve: 1.5),

        new("Keyboard and mouse for hot-desk area",
            "Two hot desks on the first floor are missing a keyboard and mouse.",
            "Hardware", TicketPriority.Low, "busisiwe", TicketStatus.Open, null, CreatedDaysAgo: 2),

        new("Sales dashboard only shows yesterday's figures",
            "The sales dashboard has not updated today's figures. The 'last refreshed' time still says yesterday 17:00.",
            "Software", TicketPriority.Medium, "emma", TicketStatus.WaitingForInformation, "sipho", CreatedDaysAgo: 3.5,
            Comments:
            [
                new("sipho", "Which report filter are you using? Please attach a screenshot of the date selector."),
            ]),

        new("Cannot connect to warehouse label printer from new PC",
            "The new PC in goods receiving cannot find the label printer. The old PC could print to it.",
            "Hardware", TicketPriority.Medium, "pieter", TicketStatus.InProgress, "ayesha", CreatedDaysAgo: 2),

        new("Add Durban depot staff to dispatch distribution list",
            "Please add the five Durban depot supervisors to the dispatch-updates distribution list.",
            "Access & Accounts", TicketPriority.Low, "ravi", TicketStatus.Resolved, "ayesha", CreatedDaysAgo: 7, HoursToResolve: 30),

        new("Slow network at Durban depot in the mornings",
            "Between 07:30 and 09:00 the depot systems are very slow. After 09:00 everything is normal.",
            "Network", TicketPriority.High, "ravi", TicketStatus.WaitingForInformation, "johan", CreatedDaysAgo: 6,
            Comments:
            [
                new("johan", "Could you run a speed test at 08:00 and again at 11:00 tomorrow and paste both results here?"),
            ]),

        new("Meeting room booking screen frozen",
            "The booking screen outside meeting room 3 has been frozen on the same image all week.",
            "Facilities", TicketPriority.Low, "emma", TicketStatus.Closed, "kagiso", CreatedDaysAgo: 35, HoursToResolve: 70),

        new("Two-factor prompt not arriving on new phone",
            "I moved to a new phone and no longer receive the sign-in approval prompt.",
            "Access & Accounts", TicketPriority.High, "thandi", TicketStatus.Open, null, CreatedDaysAgo: 0.15),

        new("Spreadsheet macros disabled after update",
            "Since this week's update, the macros in our leave-tracking spreadsheet are blocked.",
            "Software", TicketPriority.Medium, "naledi", TicketStatus.InProgress, "sipho", CreatedDaysAgo: 1.8),

        new("Broken office chair in finance",
            "One of the chairs in the finance office has a broken gas lift.",
            "Facilities", TicketPriority.Low, "thandi", TicketStatus.Closed, "ayesha", CreatedDaysAgo: 8,
            CloseNote: "Not an IT request. Logged with the building manager instead."),

        new("Laptop battery swelling, unsafe to use",
            "My laptop's trackpad is lifting and the case is bulging near the battery.",
            "Hardware", TicketPriority.Critical, "busisiwe", TicketStatus.Resolved, "johan", CreatedDaysAgo: 2.2, HoursToResolve: 6,
            Comments:
            [
                new("johan", "Replacement battery fitted and the loan laptop collected. Thanks for flagging this quickly."),
            ]),
    ];

    public static string EmailFor(string key) => $"{key}@{EmailDomain}";
}

internal sealed record DemoUser(string Key, string DisplayName, string Role, bool IsActive = true);

internal sealed record DemoComment(string AuthorKey, string Body, bool IsInternal = false);

/// <param name="HoursToResolve">Required for Resolved tickets; for Closed tickets, null means it was closed without being resolved.</param>
internal sealed record DemoTicket(
    string Title,
    string Description,
    string Category,
    TicketPriority Priority,
    string RequesterKey,
    TicketStatus Status,
    string? AgentKey,
    double CreatedDaysAgo,
    double? HoursToResolve = null,
    string? CloseNote = null,
    DemoComment[]? Comments = null);
