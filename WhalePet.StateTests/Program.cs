using System;
using System.IO;
using System.Text.Json;
using WhalePet;

static DateTimeOffset At(string time) => DateTimeOffset.Parse("2026-10-01T" + time + "+08:00");
int passed = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); passed++; }
PetState state = new(At("07:59:59"));
Check(!state.Update(At("07:59:59")), "before breakfast");
Check(state.Update(At("08:00:00")) && state.Hungry, "breakfast boundary");
state.Feed(At("08:00:00"));
Check(!state.Update(At("08:00:01")) && !state.Hungry, "feed at boundary stays fed");
Check(state.GetMood(At("08:00:04"), 30) == Mood.Eating, "eat for five seconds");
Check(state.GetMood(At("08:00:05"), 0) == Mood.Normal, "eating expiry");
Check(!state.Update(At("11:59:59")), "before lunch");
Check(state.Update(At("12:00:00")) && state.Hungry, "lunch boundary");
state.Feed(At("12:00:01"));
Check(state.Update(At("18:00:00")) && state.Hungry, "dinner boundary");
state.Feed(At("18:00:01"));
for (int n = 0; n < 5; n++) state.Touch(At("18:01:00"));
Check(state.GetMood(At("18:01:00"), 0) == Mood.Shy, "five touches");
for (int n = 0; n < 15; n++) state.Touch(At("18:01:01"));
Check(state.GetMood(At("18:01:01"), 0) == Mood.Angry, "twenty touches");
state.Update(At("18:01:15")); Check(state.Touches == 20, "before touch timeout");
state.Update(At("18:01:16")); Check(state.Touches == 0, "15 second recovery");
Check(state.GetMood(At("18:01:16"), 14.999) == Mood.Normal, "idle below threshold");
Check(state.GetMood(At("18:01:16"), 15) == Mood.Bored, "idle at threshold");
state.Touch(At("18:02:00")); state.Touch(At("18:02:15")); Check(state.Touches == 1, "new touch after pause");
PetState overnight = new(At("12:01:00"), false, At("12:00:00"));
Check(overnight.Update(DateTimeOffset.Parse("2026-10-02T01:00:00+08:00")) && overnight.Hungry, "missed dinner on wake after midnight");
PetState hungry = new(At("18:02:00"), true, At("18:00:00"));
Check(hungry.GetMood(At("18:02:00"), 40) == Mood.Hungry, "hunger before idle");
for (int n = 0; n < 20; n++) hungry.Touch(At("18:02:00"));
Check(hungry.GetMood(At("18:02:00"), 0) == Mood.Hungry, "hunger before petting");
foreach (Mood mood in Enum.GetValues<Mood>())
{
    hungry.Preview(mood, At("18:03:00"));
    Check(hungry.GetMood(At("18:03:00"), 40) == mood, "instant preview " + mood);
    hungry.Update(At("18:03:01")); Check(hungry.GetMood(At("18:03:01"), 40) == mood, "preview during refresh " + mood);
}
Check(hungry.GetMood(At("18:03:04"), 40) == Mood.Hungry, "preview expiry restores hunger");
hungry.Preview(Mood.Angry, At("18:04:00")); hungry.Touch(At("18:04:00")); Check(hungry.GetMood(At("18:04:00"), 0) == Mood.Hungry, "touch cancels preview");
hungry.Preview(Mood.Angry, At("18:04:01")); hungry.Feed(At("18:04:01")); Check(hungry.GetMood(At("18:04:01"), 0) == Mood.Eating && hungry.Touches == 0, "feed cancels preview and petting");
// Fixed UTC+8 schedule is independent of the Windows computer's timezone.
PetState utc = new(DateTimeOffset.Parse("2026-10-01T00:00:00Z"));
Check(utc.Update(DateTimeOffset.Parse("2026-10-01T00:00:00Z")), "UTC conversion triggers Beijing 08:00");
Settings settings = new() { Hungry = true, LastMealCheck = At("18:00:00"), Width = 280, X = 10, Y = 20, StartupConfigured = true };
Settings restored = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(settings))!;
PetState restart = new(At("18:05:00"), restored.Hungry, restored.LastMealCheck);
Check(restart.Hungry && !restart.Update(At("18:05:00")) && restored.Width == 280, "settings restore preserves state");
Check(LaunchCommand.Create(@"C:\Program Files\dotnet\dotnet.exe", @"D:\鲸鱼娘 桌宠\WhalePet.dll")
    == "\"C:\\Program Files\\dotnet\\dotnet.exe\" \"D:\\鲸鱼娘 桌宠\\WhalePet.dll\"", "DLL startup quotes both paths");
foreach (string invalid in new[] { "", "bad\"path" })
{
    bool rejected = false;
    try { LaunchCommand.Create(invalid, "WhalePet.dll"); } catch (ArgumentException) { rejected = true; }
    Check(rejected, "invalid startup host rejected");
}
Console.WriteLine($"PASS: {passed} state checks (meals, wake, feed, touch, idle, preview, timezone, persistence)");
