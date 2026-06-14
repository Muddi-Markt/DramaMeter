// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
namespace Muddi.DramaMeter.Blazor.Models;

public class DramaMeterSettings
{
	/// <summary>
	///     Application title (e.g. "MUDDI's").
	/// </summary>
	public string Title { get; init; } = "MUDDI's";

	/// <summary>
	///     Application subtitle (e.g. "Drama Meter").
	/// </summary>
	public string Subtitle { get; init; } = "Drama Meter";

	/// <summary>
	///     Cooldown period in minutes between votes.
	/// </summary>
	public TimeSpan CooldownPeriod { get; init; } = TimeSpan.FromMinutes(10);

	/// <summary>
	///     Labels for the four gauge segments (index 0 = leftmost).
	///     Must contain exactly 4 entries.
	/// </summary>
	public string[] GaugeLabels { get; init; } =
		{ "No Drama", "Es knistert", "Bodenlos!", "SONDERPLENUM" };
}