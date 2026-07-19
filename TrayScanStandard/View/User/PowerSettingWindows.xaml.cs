using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using TrayScanStandard.Attritubes;

namespace TrayScanStandard.View.User
{
    public partial class PowerSettingWindows : Window
    {
        private static readonly RoleEnum[] VisibleRoles = Enum.GetValues<RoleEnum>().SkipLast(1).ToArray();

        private readonly Dictionary<PowerEnum, List<CheckBox>> _checkBoxesMap = [];

        public PowerSettingWindows()
        {
            InitializeComponent();
        }

        private void PowerSettingWindows_OnLoaded(object sender, RoutedEventArgs e)
        {
            EnsurePowerTable();
            _checkBoxesMap.Clear();
            PowerPanel.Children.Clear();

            PowerPanel.Children.Add(new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 20),
                Text = Properties.Resources.PermissionsSetting,
                FontSize = 20
            });

            foreach (var power in Enum.GetValues<PowerEnum>())
            {
                if (typeof(PowerEnum).GetMember(power.ToString())[0].GetCustomAttribute<NotShowAttribute>() != null)
                {
                    continue;
                }

                _checkBoxesMap[power] = [];

                StackPanel container = new()
                {
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                TextBlock powerText = new()
                {
                    Text = Utils.Utils.GetPowerName(power),
                    FontSize = 24,
                    Margin = new Thickness(0, 10, 0, 0)
                };

                StackPanel rolePanel = new()
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                foreach (var role in VisibleRoles)
                {
                    CheckBox checkBox = new()
                    {
                        IsChecked = MainStorage.Saves.PowerTable[power].TryGetValue(role, out var isChecked) && isChecked,
                        Content = Utils.Utils.GetRoleName(role),
                        Margin = new Thickness(8, 0, 8, 0)
                    };

                    _checkBoxesMap[power].Add(checkBox);
                    rolePanel.Children.Add(checkBox);
                }

                container.Children.Add(powerText);
                container.Children.Add(rolePanel);
                PowerPanel.Children.Add(container);
            }
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            EnsurePowerTable();

            foreach (var keyValuePair in _checkBoxesMap)
            {
                for (var i = 0; i < keyValuePair.Value.Count; i++)
                {
                    MainStorage.Saves.PowerTable[keyValuePair.Key][VisibleRoles[i]] = keyValuePair.Value[i].IsChecked ?? false;
                }
            }

            MainStorage.SaveManager.Save();
            Close();
        }

        private static void EnsurePowerTable()
        {
            foreach (var power in Enum.GetValues<PowerEnum>())
            {
                if (!MainStorage.Saves.PowerTable.ContainsKey(power))
                {
                    MainStorage.Saves.PowerTable[power] = [];
                }

                foreach (var role in Enum.GetValues<RoleEnum>())
                {
                    if (!MainStorage.Saves.PowerTable[power].ContainsKey(role))
                    {
                        MainStorage.Saves.PowerTable[power][role] = false;
                    }
                }
            }
        }
    }
}
