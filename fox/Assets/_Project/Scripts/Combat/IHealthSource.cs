using System;

public interface IHealthSource
{
    string DisplayName
    {
        get;
    }
    int CurrentHealth
    {
        get;
    }
    int MaximumHealth
    {
        get;
    }
    event Action HealthChanged;
}
