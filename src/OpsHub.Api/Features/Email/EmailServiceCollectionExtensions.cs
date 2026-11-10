namespace OpsHub.Api.Features.Email;

internal static class EmailServiceCollectionExtensions
{
    public static IServiceCollection AddOpsHubEmail(this IServiceCollection services, IConfiguration configuration)
    {
        // When email is switched on, a missing sender, link address or mail server stops the app
        // at startup with a clear message, instead of failing quietly in the background later.
        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .Validate(o => !o.Enabled || !string.IsNullOrWhiteSpace(o.From),
                "Email:Enabled is true but Email:From (the sender address) is not set.")
            .Validate(o => !o.Enabled || Uri.TryCreate(o.PublicBaseUrl, UriKind.Absolute, out _),
                "Email:Enabled is true but Email:PublicBaseUrl (the address people open OpsHub at) is not a full URL.")
            .Validate(o => !o.Enabled || !string.IsNullOrWhiteSpace(o.PickupDirectory) || !string.IsNullOrWhiteSpace(o.Smtp.Host),
                "Email:Enabled is true but neither Email:Smtp:Host nor Email:PickupDirectory is set.")
            .ValidateOnStart();

        services.AddSingleton<EmailOutbox>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddHostedService<EmailDispatcher>();
        services.AddScoped<TicketNotifier>();
        return services;
    }
}
