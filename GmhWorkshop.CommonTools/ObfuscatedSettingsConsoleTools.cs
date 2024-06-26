﻿namespace GmhWorkshop.CommonTools;

public static class ObfuscatedSettingsConsoleTools
{
    public static Func<T, bool> ShouldSetPropertyIfNullOrWhiteSpace<T>(Func<T, string> propertySelector)
    {
        return settings =>
        {
            if (string.IsNullOrWhiteSpace(propertySelector(settings)))
            {
                return true;
            }

            return false;
        };
    }

    public static Func<T, (bool isValid, string message)> PropertyIsValidIfNotNullOrWhiteSpace<T>(
        Func<T, string> propertySelector)
    {
        return settings =>
        {
            if (string.IsNullOrWhiteSpace(propertySelector(settings)))
            {
                return (false, "Value can not be blank.");
            }

            return (true, string.Empty);
        };
    }

    public static Func<T, (bool isValid, string message)> PropertyIsValidIfPositiveInt<T>(Func<T, int> propertySelector)
    {
        return backupSettings =>
        {
            if (propertySelector(backupSettings) < 1)
            {
                return (false, "Value must be a positive number.");
            }

            return (true, string.Empty);
        };
    }

    public static Func<string, (bool isValid, string message)> UserEntryIsValidIfNotNullOrWhiteSpace()
    {
        return userEntry =>
        {
            if (string.IsNullOrWhiteSpace(userEntry))
            {
                return (false, "The value can not be blank.");
            }

            return (true, string.Empty);
        };
    }

    public static Func<string, (bool isValid, string message)> UserEntryIsValidIfInt()
    {
        return userEntry =>
        {
            if (string.IsNullOrWhiteSpace(userEntry))
            {
                return (false, "The value can not be blank.");
            }

            if (!int.TryParse(userEntry, out _))
            {
                return (false, "The value must be a number.");
            }

            return (true, string.Empty);
        };
    }
}