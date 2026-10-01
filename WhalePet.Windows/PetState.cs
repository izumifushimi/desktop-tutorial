using System;

namespace WhalePet;

public enum Mood { Normal, Bored, Hungry, Shy, Angry, Eating }

public sealed class PetState
{
    public bool Hungry { get; private set; }
    public DateTimeOffset LastMealCheck { get; private set; }
    public int Touches { get; private set; }
    public DateTimeOffset? LastTouch { get; private set; }
    public DateTimeOffset EatingUntil { get; private set; } = DateTimeOffset.MinValue;
    private Mood? preview;
    private DateTimeOffset previewUntil;
    private static readonly TimeSpan BeijingOffset = TimeSpan.FromHours(8);

    public PetState(DateTimeOffset now, bool hungry = false, DateTimeOffset? lastMealCheck = null)
    {
        Hungry = hungry;
        LastMealCheck = lastMealCheck ?? new DateTimeOffset(now.ToOffset(BeijingOffset).Date, BeijingOffset);
    }

    public bool Update(DateTimeOffset now)
    {
        DateTime today = now.ToOffset(BeijingOffset).Date;
        DateTimeOffset? latest = null;
        foreach (DateTime day in new[] { today.AddDays(-1), today })
            foreach (int hour in new[] { 8, 12, 18 })
            {
                DateTimeOffset slot = new(day.AddHours(hour), BeijingOffset);
                if (slot <= now) latest = slot;
            }
        bool meal = latest.HasValue && latest.Value > LastMealCheck;
        if (meal) { Hungry = true; LastMealCheck = latest!.Value; }
        if (LastTouch.HasValue && now - LastTouch.Value >= TimeSpan.FromSeconds(15))
        { Touches = 0; LastTouch = null; }
        return meal;
    }

    public void Touch(DateTimeOffset now)
    {
        CancelPreview();
        if (LastTouch.HasValue && now - LastTouch.Value >= TimeSpan.FromSeconds(15)) Touches = 0;
        Touches++;
        LastTouch = now;
    }

    public void Feed(DateTimeOffset now)
    {
        CancelPreview();
        Update(now); // A feed at a meal boundary satisfies the current meal.
        Hungry = false;
        EatingUntil = now.AddSeconds(5);
        Touches = 0;
        LastTouch = null;
    }

    public void Preview(Mood mood, DateTimeOffset now) { preview = mood; previewUntil = now.AddSeconds(4); }
    public void CancelPreview() { preview = null; }
    public Mood GetMood(DateTimeOffset now, double idleSeconds)
    {
        if (preview.HasValue && now < previewUntil) return preview.Value;
        preview = null;
        if (Hungry) return Mood.Hungry;
        if (now < EatingUntil) return Mood.Eating;
        if (Touches >= 20) return Mood.Angry;
        if (Touches >= 5) return Mood.Shy;
        return idleSeconds >= 15 ? Mood.Bored : Mood.Normal;
    }
}
