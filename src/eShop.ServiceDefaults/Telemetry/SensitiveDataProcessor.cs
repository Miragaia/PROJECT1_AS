using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace eShop.ServiceDefaults.Telemetry;

public class SensitiveDataProcessor : BaseProcessor<Activity>
{
    // Regex patterns for sensitive data
    private static readonly Regex EmailPattern =  new(@"^([^@]+)@(.+)$", RegexOptions.Compiled);
    private static readonly Regex CreditCardPattern = new(@"^(\d{6})(\d+)(\d{4})$", RegexOptions.Compiled);
    private static readonly Regex PhonePattern = new(@"^(\+\d{1,3}|\d{1,4})(\d+)(\d{2,4})$", RegexOptions.Compiled);
    private static readonly Regex CreditCardNumberPattern = new(@"^\d{13,19}$", RegexOptions.Compiled);

    private static readonly Regex IpAddressPattern = new(@"^(\d{1,3}\.\d{1,3})(\.\d{1,3}\.\d{1,3})$", RegexOptions.Compiled);

    private const string MaskReplacement = "***MASKED***";

    public override void OnEnd(Activity activity)
    {
        if (activity == null) return;

        // // Process DisplayName
        // activity.DisplayName = MaskSensitiveData(activity.DisplayName);

        // Process Tags
        foreach (var tag in activity.TagObjects)
        {
            if (tag.Key.Contains("email", StringComparison.OrdinalIgnoreCase))
            {
                activity.SetTag(tag.Key, MaskEmail(tag.Value?.ToString() ?? string.Empty));
            }
            else if ( tag.Key.Contains("user.id", StringComparison.OrdinalIgnoreCase) )
            {
                activity.SetTag(tag.Key, MaskUserId(tag.Value?.ToString() ?? string.Empty));
            }
            else if ( tag.Key.Contains("credit", StringComparison.OrdinalIgnoreCase) )
            {
                activity.SetTag(tag.Key, MaskCreditCard(tag.Value?.ToString() ?? string.Empty));
            }
            else if (tag.Key.Contains("card", StringComparison.OrdinalIgnoreCase) )
            {
                activity.SetTag(tag.Key, MaskCreditCard(tag.Value?.ToString() ?? string.Empty));
            }
            else if (tag.Key.Contains("phone", StringComparison.OrdinalIgnoreCase) )
            {
                activity.SetTag(tag.Key, MaskPhone(tag.Value?.ToString() ?? string.Empty));
            }
            else if (tag.Key.Contains("password", StringComparison.OrdinalIgnoreCase) )
            {
                activity.SetTag(tag.Key, MaskPassword(tag.Value?.ToString() ?? string.Empty));
            }
            else if (tag.Key.Contains("token", StringComparison.OrdinalIgnoreCase) )
            {
                activity.SetTag(tag.Key, MaskToken(tag.Value?.ToString() ?? string.Empty));
            }
            else if (tag.Key.Contains("auth", StringComparison.OrdinalIgnoreCase) )
            {
                activity.SetTag(tag.Key, MaskAuth(tag.Value?.ToString() ?? string.Empty));
            }
            else if (tag.Value is string value)
            {
                if (EmailPattern.IsMatch(value))
                {
                    activity.SetTag(tag.Key, MaskEmail(value));
                }
                else if (CreditCardPattern.IsMatch(value))
                {
                    activity.SetTag(tag.Key, MaskCreditCard(value));
                }
                else if (PhonePattern.IsMatch(value))
                {
                    activity.SetTag(tag.Key, MaskPhone(value));
                }
                else if (CreditCardNumberPattern.IsMatch(value))
                {
                    activity.SetTag(tag.Key, MaskCreditCard(value));
                }
                else if (IpAddressPattern.IsMatch(value))
                {
                    activity.SetTag(tag.Key, MaskIpAddress(value));
                }
            }
        }

        base.OnEnd(activity);
    }

    private string MaskEmail(string email)
    {
        var match = EmailPattern.Match(email);
        if (match.Success)
        {
            return $"{MaskReplacement}@{match.Groups[2].Value}";
        }

        return MaskReplacement;
    }

    private string MaskUserId(string userId)
    {
         if (string.IsNullOrEmpty(userId) || userId.Length <= 4){
            return "****";
         }
        return MaskReplacement;
    }

    private string MaskCreditCard(string creditCard)
    {
        var match = CreditCardPattern.Match(creditCard);
        if (match.Success)
        {
            return $"{match.Groups[1].Value}{MaskReplacement}{match.Groups[3].Value}";
        }

        return MaskReplacement;
    }

    private string MaskPhone(string phone)
    {
        var match = PhonePattern.Match(phone);
        if (match.Success)
        {
            return $"{match.Groups[1].Value}{MaskReplacement}{match.Groups[3].Value}";
        }

        return MaskReplacement;
    }

    private string MaskPassword(string password)
    {
        return MaskReplacement;
    }

    private string MaskToken(string token)
    {
        return MaskReplacement;
    }

    private string MaskAuth(string auth)
    {
        return MaskReplacement;
    }

    private string MaskIpAddress(string ipAddress)
    {
        var match = IpAddressPattern.Match(ipAddress);
        if (match.Success)
        {
            return $"{match.Groups[1].Value}{MaskReplacement}";
        }

        return MaskReplacement;
    }

}