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
    private static readonly Regex EmailPattern = new(@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}", RegexOptions.Compiled);
    private static readonly Regex CreditCardPattern = new(@"\b(?:\d{4}[-\s]?){3}\d{4}\b", RegexOptions.Compiled);
    private static readonly Regex PhonePattern = new(@"\b(?:\+\d{1,2}\s?)?\(?\d{3}\)?[-\s]?\d{3}[-\s]?\d{4}\b", RegexOptions.Compiled);
    private static readonly Regex UserIdPattern = new(@"user[\w-]+", RegexOptions.Compiled);

    private const string MaskReplacement = "***MASKED***";

    public override void OnEnd(Activity activity)
    {
        if (activity == null) return;

        // Process DisplayName
        activity.DisplayName = MaskSensitiveData(activity.DisplayName);

        // Process Tags
        foreach (var tag in activity.Tags.ToList())
        {
            if (tag.Value != null && IsSensitiveKey(tag.Key))
            {
                // Remove the existing tag and add a masked version
                activity.SetTag(tag.Key, MaskSensitiveData(tag.Value.ToString() ?? string.Empty));
            }
        }

        // We can't modify ActivityEvents directly, so we'll just process them in-place without creating new events
        // In a real implementation, you might want to implement a more sophisticated approach

        base.OnEnd(activity);
    }

    private bool IsSensitiveKey(string key)
    {
        // Define which keys should be considered sensitive
        return key.Contains("email", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("user", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("credit", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("card", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("phone", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("password", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("token", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("auth", StringComparison.OrdinalIgnoreCase);
    }

    private string MaskSensitiveData(string input)
    {
        if (string.IsNullOrEmpty(input)) 
            return input;

        // Mask email addresses
        input = EmailPattern.Replace(input, MaskReplacement);
        
        // Mask credit card numbers
        input = CreditCardPattern.Replace(input, MaskReplacement);
        
        // Mask phone numbers
        input = PhonePattern.Replace(input, MaskReplacement);
        
        // Mask user IDs
        input = UserIdPattern.Replace(input, MaskReplacement);

        return input;
    }
}