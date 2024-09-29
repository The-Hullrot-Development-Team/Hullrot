using Content.Client.Message;
using Content.Client.Stylesheets.Redux;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Monitor;
using Content.Shared.Temperature;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client.Atmos.Monitor.UI.Widgets;

[GenerateTypedNameReferences]
public sealed partial class ThresholdBoundControl : BoxContainer
{
    // raw values to use in thresholds, prefer these
    // over directly setting Modified(Value/LastValue)
    // when working with the FloatSpinBox
    private float _value;

    // convenience thing for getting multiplied values
    // and also setting value to a usable value
    private float ScaledValue
    {
        get => _value * _uiValueScale;
        set => _value = value / _uiValueScale;
    }

    private float _uiValueScale;

    public event Action? OnValidBoundChanged;
    public Action<float>? OnBoundChanged;
    public Action<bool>? OnBoundEnabled;

    public void SetValue(float value)
    {
        _value = value;
        CSpinner.Value = ScaledValue;
    }

    public void SetEnabled(bool enabled)
    {
        CBoundEnabled.Pressed = enabled;

        if (enabled)
        {
            CBoundLabel.RemoveStyleClass(StyleClass.LabelWeak);
        }
        else
        {
            CBoundLabel.SetOnlyStyleClass(StyleClass.LabelWeak);
        }
    }

    public void SetWarningState(AtmosAlarmType alarm)
    {
        if(alarm == AtmosAlarmType.Normal)
        {
            CBoundLabel.FontColorOverride = null;
        }
        else
        {
            CBoundLabel.FontColorOverride = AirAlarmWindow.ColorForAlarm(alarm);
        }
    }

    public ThresholdBoundControl(string controlLabel, float value, float uiValueScale = 1)
    {
        RobustXamlLoader.Load(this);

        _uiValueScale = uiValueScale > 0 ? uiValueScale : 1;
        _value = value;

        CBoundLabel.Text = controlLabel;

        CSpinner.Value = ScaledValue;

        CSpinner.OnValueChanged += SpinnerValueChanged;
        CBoundEnabled.OnToggled += CheckboxToggled;
    }

    private void SpinnerValueChanged(FloatSpinBox.FloatSpinBoxEventArgs args)
    {
        // ensure that the value in the spinbox is transformed
        ScaledValue = args.Value;
        // set the value in the scope above
        OnBoundChanged!(_value);
        OnValidBoundChanged!.Invoke();
    }

    private void CheckboxToggled(BaseButton.ButtonToggledEventArgs args)
    {
        OnBoundEnabled!(args.Pressed);
        OnValidBoundChanged!.Invoke();
    }
}
