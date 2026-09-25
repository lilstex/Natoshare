namespace Natoshare.Domain.Admin;

// A small knob the team can turn without a deploy, like how many days a trial lasts.
// Kept as plain string key/value pairs since the list of settings is short and each
// one means something different, there is no need for a fancy schema here.
public class SystemSetting
{
    public string Key { get; init; } = "";

    public string Value { get; set; } = "";

    public DateTimeOffset UpdatedAt { get; set; }

    public Guid? UpdatedByAdminUserId { get; set; }
}
