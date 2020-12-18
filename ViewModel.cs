// ReSharper disable StringLiteralTypo
// ReSharper disable UnusedMember.Global

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Sudoku.Annotations;

namespace Sudoku
{
    public class ViewModel : INotifyPropertyChanged
    {
        #region Fields

        private bool _initiallyExecuted;
        private bool _started;
        private bool _canClickSolutionOrder;
        private CancellationTokenSource _cts;
        private string _path = Assembly.GetExecutingAssembly().Location.
            Replace(".dll", ".sdk").Replace(".exe", ".sdk");

        private int _delay = 100;
        private DisplayTextTypeEnum _displayTextType = DisplayTextTypeEnum.Value;

        #endregion

        #region Event Handler

        public event EventHandler<FileDialogArgs> OpenFile;
        public event EventHandler<FileDialogArgs> SaveFile;
        public event EventHandler<Exception> Error;

        #endregion

        #region Ctor

        public ViewModel()
        {
            Fields = new ObservableCollection<Field>();
            Columns = new ObservableCollection<ObservableCollection<Field>>();
            Rows = new ObservableCollection<ObservableCollection<Field>>();
            Squares = new ObservableCollection<ObservableCollection<Field>>();

            // Create Areas
            for (var v = 0; v < 9; v++)
            {
                Columns.Add(new ObservableCollection<Field>());
                Rows.Add(new ObservableCollection<Field>());
                Squares.Add(new ObservableCollection<Field>());
            }

            // Create Fields
            for (var f = 0; f < 81; f++)
            {
                var col = f % 9;
                var row = f / 9;
                var sqrCol = col / 3;
                var sqrRow = row / 3;
                var sqr = sqrCol + 3 * sqrRow;

                var field = new Field(f, col, row, sqr);
                Fields.Add(field);
                Columns[col].Add(field);
                Rows[row].Add(field);
                Squares[sqr].Add(field);
            }

            // Create Commands
            OpenCommand = new AsyncCommand<object>(ExecuteOpen, CanExecuteOpenCommand);
            SaveCommand = new AsyncCommand<object>(ExecuteSave, CanExecuteSaveCommand);
            ResetCommand = new AsyncCommand<object>(ExecuteReset, CanExecuteResetCommand);
            StartCommand = new AsyncCommand<object>(ExecuteStart, CanExecuteStartCommand);
            StopCommand = new AsyncCommand<object>(ExecuteStop, CanExecuteStopCommand);
        }

        #endregion

        #region Labels

        public string Title => "Sudoku Solver";
        public string OpenLabel => "Open";
        public string SaveLabel => "Save";
        public string ResetLabel => "Reset";
        public string StartLabel => "Start";
        public string StartSlowMotionLabel => "Start Slow Motion";
        public string StopLabel => "Stop";
        public string SolutionLabel => "Show Order";
        public string PrintLabel => "Print";
        public string DelayLabel => "Delay (ms)";

        #endregion

        #region Public Properties

        public bool InitiallyExecuted
        {
            get => _initiallyExecuted;
            set
            {
                _initiallyExecuted = value;
                if (_initiallyExecuted)
                    CanClickSolutionOrder = true;
                OnPropertyChanged(nameof(InitiallyExecuted));
            }
        }

        public bool Started
        {
            get => _started;
            set
            {
                _started = value;
                OnPropertyChanged(nameof(Started));
            }
        }

        public bool CanClickSolutionOrder
        {
            get => _canClickSolutionOrder;
            set
            {
                _canClickSolutionOrder = value;
                OnPropertyChanged(nameof(CanClickSolutionOrder));
            }
        }

        public int Delay
        {
            get => _delay;
            set
            {
                _delay = value;
                OnPropertyChanged(nameof(Delay));
            }
        }

        public DisplayTextTypeEnum DisplayTextType
        {
            get => _displayTextType;
            set
            {
                _displayTextType = value;
                OnPropertyChanged(nameof(DisplayTextType));
            }
        }

        public int Progress { get; private set; }

        public Visibility ProgressBarVisibility { get; private set; } = Visibility.Collapsed;

        public ObservableCollection<Field> Fields { get; }

        public ObservableCollection<ObservableCollection<Field>> Columns { get; }

        public ObservableCollection<ObservableCollection<Field>> Rows { get; }

        public ObservableCollection<ObservableCollection<Field>> Squares { get; }

        #endregion

        #region Commands

        public IAsyncCommand<object> OpenCommand { get; }
        public IAsyncCommand<object> SaveCommand { get; }
        public IAsyncCommand<object> ResetCommand { get; }
        public IAsyncCommand<object> StartCommand { get; }
        public IAsyncCommand<object> StopCommand { get; }

        private bool CanExecuteOpenCommand(object parameter) => DisplayTextType == DisplayTextTypeEnum.Value;
        internal async Task ExecuteOpen(object parameter)
        {
            if (OpenFile == null)
                return;

            try
            {
                string dir = null;
                string file = null;
                if (!string.IsNullOrEmpty(_path) && File.Exists(_path))
                {
                    file = _path;
                    dir = new FileInfo(file).DirectoryName;
                }
                if (string.IsNullOrEmpty(dir))
                {
                    dir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                }

                var args = new FileDialogArgs
                {
                    Filter = "Sudoku-Dateien (*.sdk)|*.sdk",
                    InitialDirectory = dir,
                    FileName = file
                };
                OpenFile(this, args);

                if (!args.Result == true)
                    return;

                _path = args.FileName;

                await Load((await File.ReadAllTextAsync(_path, Encoding.UTF8)).Split(','));
            }
            catch (Exception exception)
            {
                Error?.Invoke(this, exception);
            }
        }

        private bool CanExecuteSaveCommand(object parameter) => DisplayTextType == DisplayTextTypeEnum.Value;
        private async Task ExecuteSave(object parameter)
        {
            if (SaveFile == null)
                return;

            try
            {
                string dir = null;
                string file = null;
                if (!string.IsNullOrEmpty(_path) && File.Exists(_path))
                {
                    file = _path;
                    dir = new FileInfo(file).DirectoryName;
                }
                if (string.IsNullOrEmpty(dir))
                {
                    dir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                }

                var args = new FileDialogArgs
                {
                    Filter = "Sudoku-Dateien (*.sdk)|*.sdk",
                    DefaultExt = "*.sdk",
                    InitialDirectory = dir,
                    OverwritePrompt = true,
                    FileName = file
                };
                SaveFile(this, args);

                if (!args.Result == true)
                    return;

                _path = args.FileName;

                var sb = new StringBuilder();
                foreach (var field in Fields)
                {
                    sb.Append($"{field.Value},");
                }

                await File.WriteAllTextAsync(_path, sb.ToString().Trim(','), Encoding.UTF8);
            }
            catch (Exception exception)
            {
                Error?.Invoke(this, exception);
            }
        }

        private bool CanExecuteResetCommand(object parameter) => Fields.Any(f => f.Value.HasValue) &&
                                                                 InitiallyExecuted &&
                                                                 DisplayTextType == DisplayTextTypeEnum.Value;
        private async Task ExecuteReset(object parameter)
        {
            await Reset();
        }

        private bool CanExecuteStartCommand(object parameter) => Fields.Any(f => f.StartValue > 0) && 
                                                                 !Fields.All(f => f.Value.HasValue) && 
                                                                 DisplayTextType == DisplayTextTypeEnum.Value;
        private async Task ExecuteStart(object parameter)
        {
            Started = true;

            /*
            var delay = Delay;
            if (parameter != null)
            {
                int.TryParse(parameter.ToString(), out delay);
            }
            */

            _cts = new CancellationTokenSource();
            await Start(_cts.Token);

            Started = false;
            CommandManager.InvalidateRequerySuggested();
        }

        private bool CanExecuteStopCommand(object parameter) => Started &&
                                                                DisplayTextType == DisplayTextTypeEnum.Value;
        private async Task ExecuteStop(object parameter)
        {
            await Task.Run(() => _cts?.Cancel());

            Progress = 0;
            OnPropertyChanged(nameof(Progress));
            ProgressBarVisibility = Visibility.Collapsed;
            OnPropertyChanged(nameof(ProgressBarVisibility));

            Started = false;
            OnPropertyChanged();
        }

        #endregion

        #region Public Methods

        public async Task Start(CancellationToken ct = default)
        {
            Fields.AsParallel().ForAll(field => field.Init());
            OnPropertyChanged();

            if (Delay > 0)
            {
                Progress = 0;
                OnPropertyChanged(nameof(Progress));
                ProgressBarVisibility = Visibility.Visible;
                OnPropertyChanged(nameof(ProgressBarVisibility));
            }

            await Solve(0, ct);

            if (Delay > 0)
            {
                Progress = 0;
                OnPropertyChanged(nameof(Progress));
                ProgressBarVisibility = Visibility.Collapsed;
                OnPropertyChanged(nameof(ProgressBarVisibility));
            }

            Validate();

            InitiallyExecuted = true;
        }

        public async Task Reset(CancellationToken ct = default)
        {
            await Task.Run(() => Fields.AsParallel().ForAll(field => field.Reset()), ct);

            OnPropertyChanged();
        }

        public async Task Load(string[] values, CancellationToken ct = default)
        {
            await Task.Run(() =>
            {
                for (var v = 0; v < values.Length; v++)
                {
                    if (ct.IsCancellationRequested)
                        return;

                    int.TryParse(values[v], out var value);
                    Fields[v].Load(value);
                }
            }, ct);
        }

        #endregion

        #region PropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region Private Methods

        private async Task Solve(int solvedOrder, CancellationToken ct = default)
        {
            if (ct.IsCancellationRequested)
                return;

            solvedOrder++;

            if (solvedOrder < 81)
                Progress = solvedOrder;

            if (Delay > 0)
            {
                await Task.Delay(Delay, ct);
                OnPropertyChanged(nameof(Progress));
            }

            if (await HandleDirectPossibleValues(Squares, solvedOrder, ct))
                return;
            if (await HandleDirectPossibleValues(Rows, solvedOrder, ct))
                return;
            if (await HandleDirectPossibleValues(Columns, solvedOrder, ct))
                return;

            if (await HandleRelatedPossibleValues(Squares, solvedOrder, ct))
                return;
            if (await HandleRelatedPossibleValues(Rows, solvedOrder, ct))
                return;
            if (await HandleRelatedPossibleValues(Columns, solvedOrder, ct))
                return;


            if (await HandleSquareRelatedAreasPossibleValues(Squares, solvedOrder, ct))
                return;

            OnPropertyChanged();
        }

        private void Validate()
        {
            try
            {
                var areaTypes = new List<ObservableCollection<ObservableCollection<Field>>> { Columns, Rows, Squares };
                foreach (var areas in areaTypes)
                {
                    foreach (var area in areas)
                    {
                        var value = 0;
                        var fields = area.OrderBy(f => f.Value).ToList();
                        foreach (var field in fields)
                        {
                            value++;
                            if (field.Value != value)
                            {
                                throw new ApplicationException($"Error in {field}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, ex);
            }
        }

        private async Task<bool> HandleDirectPossibleValues(
            IEnumerable<ObservableCollection<Field>> range, 
            int solvedOrder, CancellationToken ct)
        {
            foreach (var fields in range)
            {
                if (ct.IsCancellationRequested)
                    return true;

                foreach (var field1 in fields)
                {
                    if (ct.IsCancellationRequested)
                        return true;

                    foreach (var field2 in fields)
                    {
                        if (ct.IsCancellationRequested)
                            return true;

                        field1.RemovePossibleValue(field2.Value);
                    }

                    if (!field1.SetPossibleValue(solvedOrder, ct))
                        continue;

                    await Solve(solvedOrder, ct);

                    return true;
                }
            }

            return false;
        }

        private async Task<bool> HandleRelatedPossibleValues(
            IEnumerable<ObservableCollection<Field>> range, 
            int solvedOrder, CancellationToken ct)
        {
            foreach (var fields in range)
            {
                if (ct.IsCancellationRequested)
                    return true;

                if (!await HandleRelatedPossibleValues(fields, solvedOrder, ct))
                    continue;

                await Solve(solvedOrder, ct);

                return true;
            }

            return false;
        }

        private static async Task<bool> HandleRelatedPossibleValues(
            IReadOnlyCollection<Field> fields, 
            int solvedOrder, CancellationToken ct)
        {
            return await Task.Run(() =>
                {
                    for (var n = 1; n < 10; n++)
                    {
                        if (ct.IsCancellationRequested)
                            return true;

                        var containingFields = fields.Where(field => field.ContainsPossibleValue(n)).ToList();
                        if (containingFields.Count != 1)
                            continue;

                        var containingField = containingFields.First();
                        containingField.SetValue(n, solvedOrder, ct);

                        return true;
                    }

                    return false;
                }, ct);
        }

        private async Task<bool> HandleSquareRelatedAreasPossibleValues(
            IEnumerable<ObservableCollection<Field>> squares,
            int solvedOrder, CancellationToken ct)
        {
            foreach (var square in squares)
            {
                for (var n = 1; n < 10; n++)
                {
                    if (square.Any(f => f.Value == n))
                        continue;

                    var value = n;
                    var possibleFields = square.Where(f => 
                        f.ContainsPossibleValue(value)).ToList();
                    if (possibleFields.Count == 0)
                        continue;

                    var firstPossibleField = possibleFields.First();
                    var s = firstPossibleField.Square;
                    var c = firstPossibleField.Column;
                    var r = firstPossibleField.Row;

                    var skip = true;
                    var skipColumn = false;
                    var skipRow = false;

                    foreach (var possibleField in possibleFields)
                    {
                        if (c != possibleField.Column)
                            skipColumn = true;
                        if (r != possibleField.Row)
                            skipRow = true;
                    }

                    if (!skipColumn)
                    {
                        var column = Columns[c];
                        foreach (var field in column)
                        {
                            if (field.Square == s)
                                continue;

                            field.RemovePossibleValue(n);
                            skip = false;
                        }
                    }
                    if (!skipRow)
                    {
                        var row = Rows[r];

                        foreach (var field in row)
                        {
                            if (field.Square == s)
                                continue;

                            field.RemovePossibleValue(n);
                            skip = false;
                        }
                    }

                    if (skip)
                    {
                        continue;
                    }

                    await Solve(solvedOrder, ct);

                    return true;
                }
            }

            return false;
        }

        #endregion
    }
    public enum DisplayTextTypeEnum
    {
        Value,
        SolvedOrder
    }

    public class Field : INotifyPropertyChanged
    {
        #region Constants

        private const string ValuePrefix = "_";

        #endregion

        #region Fields

        private int _startValue;
        private int? _value;
        private int _solvedOrder;

        #endregion

        #region Ctor

        public Field(int order, int col, int row, int sqr)
        {
            Order = order;
            Column = col;
            Row = row;
            Square = sqr;

            ResetPossibleValues();
        }

        #endregion

        #region Public Properties

        public double FontSize => IsLocked ?
            16 : 12;

        public FontWeight FontWeight => IsLocked ?
            FontWeights.Bold :
            FontWeights.Normal;

        public SolidColorBrush BackColor => IsLocked ?
            new SolidColorBrush(Colors.MediumSeaGreen) :
            new SolidColorBrush(Colors.Transparent);

        public int Order { get; }
        public int Column { get; }
        public int Row { get; }
        public int Square { get; }

        public int SolvedOrder
        {
            get => _solvedOrder;
            set
            {
                _solvedOrder = value;

                OnPropertyChanged(nameof(SolvedOrder));
                OnPropertyChanged(nameof(ToolTip));
            }
        }

        public int? Value
        {
            get => _value;
            set
            {
                _value = value;

                if (_value.HasValue && _value.Value > 0)
                {
                    ClearPossibleValues();
                }

                OnPropertyChanged(nameof(Value));
            }
        }

        public int StartValue
        {
            get => _startValue;
            set
            {
                _startValue = value;
                OnPropertyChanged(nameof(StartValue));
            }
        }

        public bool IsLocked => StartValue > 0;

        public List<string> PossibleValues { get; set; }

        public int PossibleValue =>
            PossibleValues.Count == 1 ?
                int.Parse(PossibleValues[0].Replace(ValuePrefix, "")) : 0;

        public string ToolTip => $"Field {Order+1}, SolvedOrder {SolvedOrder}" + Environment.NewLine +
                                 $"Column {Column+1}, Row {Row+1}, Square {Square+1}" + Environment.NewLine +
                                 $"Value {Value}, PossibleValues ({(IsLocked ? "Is default value" : string.Join(',', PossibleValues).Replace(ValuePrefix, " "))})";
        #endregion

        #region PropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void RaisePropertiesChanged()
        {
            OnPropertyChanged(nameof(Value));
            OnPropertyChanged(nameof(StartValue));
            OnPropertyChanged(nameof(IsLocked));
            OnPropertyChanged(nameof(FontWeight));
            OnPropertyChanged(nameof(FontSize));
            OnPropertyChanged(nameof(BackColor));
            OnPropertyChanged(nameof(ToolTip));
            RaisePossibleValuesChanged();
        }

        #endregion

        #region Public Methods

        public override string ToString()
        {
            return $"Field {Order} = {Value}, possible Values = ({ToolTip})";
        }

        public void Init()
        {
            if (Value.HasValue)
                StartValue = Value.Value;

            SolvedOrder = 0;

            RaisePropertiesChanged();
        }

        public void Reset()
        {
            Load(StartValue);
        }

        public void Load(int? value)
        {
            if (value == 0)
                value = null;

            ResetPossibleValues();

            Value = value;
            StartValue = Value ?? 0;
            SolvedOrder = 0;

            RaisePropertiesChanged();
        }

        #endregion

        #region Possible Values Handling

        private void ResetPossibleValues()
        {
            PossibleValues = new List<string>();
            for (var v = 1; v < 10; v++)
            {
                PossibleValues.Add(ValuePrefix + v);
            }

            RaisePossibleValuesChanged();
        }

        public bool SetPossibleValue(int solvedOrder, CancellationToken ct)
        {
            if (PossibleValues.Count != 1)
                return false;

            SetValue(PossibleValue, solvedOrder, ct);

            return true;
        }

        public void SetValue(int value, int solvedOrder, CancellationToken ct)
        {
            Value = value;
            SolvedOrder = solvedOrder;
        }

        public void ClearPossibleValues()
        {
            PossibleValues.Clear();
            RaisePossibleValuesChanged();
        }

        public void RemovePossibleValue(int? value)
        {
            if (value == null)
                return;

            if (!PossibleValues.Contains(ValuePrefix + value))
                return;

            PossibleValues.Remove(ValuePrefix + value);
            RaisePossibleValuesChanged();
        }

        public bool ContainsPossibleValue(int value)
        {
            return PossibleValues.Contains(ValuePrefix + value);
        }

        public void RaisePossibleValuesChanged()
        {
            OnPropertyChanged(nameof(PossibleValues));
            OnPropertyChanged(nameof(ToolTip));
        }

        #endregion
    }

    public class FileDialogArgs : EventArgs
    {
        public string Filter { get; set; }
        public string DefaultExt { get; set; }
        public string InitialDirectory { get; set; }
        public bool OverwritePrompt { get; set; }
        public string FileName { get; set; }
        public bool? Result { get; set; }
    }
}
