using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

using Microsoft.Win32;

namespace Sudoku
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// ReSharper disable once UnusedMember.Global
    public partial class MainWindow
    {
        private List<Control> _controls;

        public MainWindow()
        {
            InitializeComponent();

            DataContext = new ViewModel();

            CreateInputControls();

            if (!(DataContext is ViewModel vm)) 
                return;

            vm.OpenFile += OnOpenFile;
            vm.SaveFile += OnSaveFile;
            vm.Error += OnError;

            vm.ExecuteOpen(null).ConfigureAwait(true);
        }

        private void CreateInputControls()
        {
            _controls = new List<Control>();
            var i = 0;
            for (var r = 0; r < 9; r++)
            {
                for (var c = 0; c < 9; c++)
                {
                    var control = CreateInputControl();
                    CreateDynamicBinding(DataContext, i, "Value", control, TextBox.TextProperty, BindingMode.TwoWay);
                    CreateDynamicBinding(DataContext, i, "FontWeight", control, FontWeightProperty, BindingMode.OneWay);
                    CreateDynamicBinding(DataContext, i, "FontSize", control, FontSizeProperty, BindingMode.OneWay);
                    CreateDynamicBinding(DataContext, i, "BackColor", control, BackgroundProperty, BindingMode.OneWay);
                    CreateDynamicBinding(DataContext, i, "ToolTip", control, ToolTipProperty, BindingMode.OneWay);
                    CreateDynamicBinding(DataContext, i, "IsLocked", control, TextBoxBase.IsReadOnlyProperty,
                        BindingMode.OneWay);

                    SetBorders(control, c, r);
                    Grid.SetRow(control, r);
                    Grid.SetColumn(control, c);
                    GridMain.Children.Add(control);
                    _controls.Add(control);
                    i++;
                }
            }
        }

        private static Control CreateInputControl()
        {
            var txt = new TextBox
            {
                TextAlignment = TextAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                BorderBrush = new SolidColorBrush(Colors.Black),
                BorderThickness = new Thickness(0.5),
                Margin = new Thickness(0)
            };
            txt.GotFocus += OnFieldGotFocus;
            return txt;
        }

        private static void CreateDynamicBinding(object source, int i, string propertyName, 
            DependencyObject dependencyObject, DependencyProperty dependencyProperty, BindingMode bindingMode)
        {
            var binding = new Binding
            {
                Source = source,
                Path = new PropertyPath($"Fields[{i}].{propertyName}"),
                Mode = bindingMode,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };
            BindingOperations.SetBinding(dependencyObject, dependencyProperty, binding);
        }

        private static void SetBorders(Control control, int c, int r)
        {
            if (c == 2 || c == 5)
            {
                control.BorderThickness = new Thickness(control.BorderThickness.Left,
                    control.BorderThickness.Top, 2, control.BorderThickness.Bottom);
            }

            if (r == 3 || r == 6)
            {
                control.BorderThickness = new Thickness(control.BorderThickness.Left,
                    2, control.BorderThickness.Right, control.BorderThickness.Bottom);
            }
        }

        private void OnOpenFile(object sender, FileDialogArgs e)
        {
            try
            {
                var fileDialog = new OpenFileDialog
                {
                    Filter = e.Filter,
                    DefaultExt = e.DefaultExt,
                    InitialDirectory = e.InitialDirectory
                };

                if (!string.IsNullOrEmpty(e.FileName))
                {
                    fileDialog.FileName = e.FileName;
                }

                if (fileDialog.ShowDialog() == true)
                {
                    e.FileName = fileDialog.FileName;
                    e.Result = true;
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Exception", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            e.FileName = null;
            e.Result = false;
        }

        private void OnSaveFile(object sender, FileDialogArgs e)
        {
            try
            {
                var fileDialog = new SaveFileDialog
                {
                    Filter = e.Filter,
                    DefaultExt = e.DefaultExt,
                    InitialDirectory = e.InitialDirectory,
                    OverwritePrompt = e.OverwritePrompt
                };

                if (!string.IsNullOrEmpty(e.FileName))
                {
                    fileDialog.FileName = e.FileName;
                }

                if (fileDialog.ShowDialog() == true)
                {
                    e.FileName = fileDialog.FileName;
                    e.Result = true;
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Exception", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            e.FileName = null;
            e.Result = false;
        }

        private void OnError(object sender, Exception e)
        {
            MessageBox.Show(this, e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private static void OnFieldGotFocus(object sender, RoutedEventArgs e)
        {
            if (!(sender is TextBox tb)) return;
            if (string.IsNullOrEmpty(tb.Text)) return;
            tb.SelectionStart = 0;
            tb.SelectionLength = tb.Text.Length;
        }

        private void ButtonBind_OnClick(object sender, RoutedEventArgs e)
        {
            if (!(DataContext is ViewModel vm))
                return;

            vm.DisplayTextType = vm.DisplayTextType == DisplayTextTypeEnum.Value ? 
                DisplayTextTypeEnum.SolvedOrder : DisplayTextTypeEnum.Value;

            for (var i = 0; i < 81; i++)
            {
                var control = _controls[i];
                BindingOperations.ClearBinding(control, TextBlock.TextProperty);
                CreateDynamicBinding(DataContext, i,
                    vm.DisplayTextType == DisplayTextTypeEnum.Value ? "Value" : "SolvedOrder", control,
                    TextBox.TextProperty, BindingMode.TwoWay);
            }
        }

        private void ButtonPrint_OnClick(object sender, RoutedEventArgs e)
        {
            var margin = GridMain.Margin;
            try
            {
                GridMain.Margin = new Thickness(40);
                var dialog = new PrintDialog();
                if (dialog.ShowDialog() != true) return;
                dialog.PrintVisual(GridMain, Title);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Exception",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                GridMain.Margin = margin;
            }
        }
    }
}
