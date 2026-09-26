using System;
using System.Collections.Generic;
using Stellar.Abstractions.Services;
using Stellar.PhiloLens.UI;
using Xunit;

namespace Stellar.PhiloLens.Tests;

public sealed class ProfileCardButtonTests
{
    private readonly FakeCardActions _actions = new();
    private readonly FakeLocalization _localization = new();

    [Fact]
    public void Registers_the_button_with_the_current_language_label()
    {
        _localization.Label = "Lens";

        using var button = new ProfileCardButton(_actions, _localization, _ => { });

        Assert.Equal(new[] { "Lens" }, _actions.LiveLabels());
    }

    [Fact]
    public void Relabels_the_button_when_the_language_changes()
    {
        using var button = new ProfileCardButton(_actions, _localization, _ => { });

        _localization.Switch("Lente");

        Assert.Equal(new[] { "Lente" }, _actions.LiveLabels());
    }

    [Fact]
    public void Dispose_removes_the_button_and_stops_listening()
    {
        var button = new ProfileCardButton(_actions, _localization, _ => { });

        button.Dispose();
        _localization.Switch("Lente");

        Assert.Empty(_actions.LiveLabels());
    }

    private sealed class FakeCardActions : IProfileCardActions
    {
        private readonly List<Registration> _registrations = new();

        public IDisposable Register(ProfileCardActionSpec spec)
        {
            var registration = new Registration(spec.Label);
            _registrations.Add(registration);
            return registration;
        }

        public List<string> LiveLabels() => _registrations.FindAll(r => !r.Disposed).ConvertAll(r => r.Label);

        private sealed class Registration : IDisposable
        {
            public Registration(string label) => Label = label;

            public string Label { get; }
            public bool Disposed { get; private set; }

            public void Dispose() => Disposed = true;
        }
    }

    private sealed class FakeLocalization : ILocalization
    {
        public string Label { get; set; } = "Lens";
        public string Language { get; private set; } = "en";

        public event Action? LanguageChanged;

        public string T(string key) => Label;
        public string TFormat(string key, params object[] args) => Label;

        public void Switch(string label)
        {
            Label = label;
            Language = "id";
            LanguageChanged?.Invoke();
        }
    }
}
