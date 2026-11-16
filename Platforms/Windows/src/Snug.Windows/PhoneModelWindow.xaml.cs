using System.Windows;
using Snug.Windows.Core;

namespace Snug.Windows;

/// <summary>The desktop settings host uses the exact same picker as the UU phone screen.</summary>
public partial class PhoneModelWindow : Window
{
    public PhoneChoice? SelectedChoice { get; private set; }

    public PhoneModelWindow(PhoneCatalog catalog, PhoneChoice current)
    {
        InitializeComponent();
        Picker.Configure(catalog, current);
        Picker.ChoiceSelected += (_, choice) =>
        {
            SelectedChoice = choice;
            DialogResult = true;
        };
        Picker.CloseRequested += (_, _) => DialogResult = false;
    }
}
