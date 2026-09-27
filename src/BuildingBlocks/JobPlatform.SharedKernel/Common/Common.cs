using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.SharedKernel.Common.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ActorType
    {
        JobSeeker,
        Employer,
        Administrator,
        ExternalJobSite,
        Guest,
        System
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Language
    {
        Ar,
        En
    }
}

namespace JobPlatform.SharedKernel.Common.ValueObjects
{
    public sealed partial class Email : ValueObject
    {
        public const int MaxLength = 254;

        private Email(string value) => Value = value;

        public string Value { get; }

        public static bool TryCreate(string? input, out Email? email)
        {
            email = null;
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            var normalised = input.Trim().ToLowerInvariant();
            if (normalised.Length > MaxLength || !EmailPattern().IsMatch(normalised))
            {
                return false;
            }

            email = new Email(normalised);
            return true;
        }

        public static Email Create(string input) =>
            TryCreate(input, out var email) ? email! : throw new ArgumentException("Invalid email address.", nameof(input));

        public override string ToString() => Value;

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Value;
        }

        [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant)]
        private static partial Regex EmailPattern();
    }

    /// <summary>E.164 mobile number (+ followed by 7 to 15 digits).</summary>
    public sealed partial class MobileNumber : ValueObject
    {
        private MobileNumber(string value) => Value = value;

        public string Value { get; }

        public static bool TryCreate(string? input, out MobileNumber? number)
        {
            number = null;
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            var normalised = new string(input.Where(c => !char.IsWhiteSpace(c) && c != '-' && c != '(' && c != ')').ToArray());
            if (!E164Pattern().IsMatch(normalised))
            {
                return false;
            }

            number = new MobileNumber(normalised);
            return true;
        }

        public static MobileNumber Create(string input) =>
            TryCreate(input, out var number) ? number! : throw new ArgumentException("Invalid mobile number.", nameof(input));

        /// <summary>Masked form safe for logs, e.g. +970****4567.</summary>
        public string Masked => Value.Length <= 6 ? "****" : string.Concat(Value.AsSpan(0, 4), "****", Value.AsSpan(Value.Length - 4));

        public override string ToString() => Value;

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Value;
        }

        [GeneratedRegex(@"^\+[1-9]\d{6,14}$", RegexOptions.CultureInvariant)]
        private static partial Regex E164Pattern();
    }

    public sealed class LocalizedText : ValueObject
    {
        public LocalizedText(string ar, string en)
        {
            Ar = ar;
            En = en;
        }

        public string Ar { get; }
        public string En { get; }

        public string For(Enums.Language language) => language == Enums.Language.Ar ? Ar : En;

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Ar;
            yield return En;
        }
    }
}
