using Microsoft.Xaml.Behaviors;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace DatePickerBehavior
{
    public class DatePickerBehavior : Behavior<DatePicker>
    {
        #region 内部データ
        private string TempText;
        private string ViewText;
        private bool IscustomizeFormat;
        private DatePickerTextBox _textBox;
        private IDictionary<DependencyProperty, bool> _isHandlerSuspended;
        private Button _calendarButton;
        #endregion

        #region DependencyProperties
        #region IsKeyEnterToNextControl
        /// <summary>
        /// Enterキーでフォーカス移動するフラグ
        /// </summary>
        public bool IsKeyEnterToNextControl
        {
            get { return (bool)GetValue(IsKeyEnterToNextControlProperty); }
            set { SetValue(IsKeyEnterToNextControlProperty, value); }
        }

        public static readonly DependencyProperty IsKeyEnterToNextControlProperty =
            DependencyProperty.Register("IsKeyEnterToNextControl", typeof(bool), typeof(DatePickerBehavior),
                                        new FrameworkPropertyMetadata(false));
        #endregion

        #region IsMonthYear
        /// <summary>
        /// カレンダーを年月までとするフラグ
        /// </summary>
        public static readonly DependencyProperty IsMonthYearProperty =
            DependencyProperty.Register("IsMonthYear", typeof(bool), typeof(DatePickerBehavior),
                                         new FrameworkPropertyMetadata(false));
        public bool IsMonthYear
        {
            get { return (bool)GetValue(IsMonthYearProperty); }
            set { SetValue(IsMonthYearProperty, value); }
        }
        #endregion

        #region 和暦用
        private System.Globalization.JapaneseCalendar jpCalendar;
        private System.Globalization.CultureInfo cultureInfo;
        #endregion

        #region カスタム書式
        /// <summary>
        /// 書式
        /// ggy年MM月dd日(ddd)
        /// ggy年MM月dd日
        /// ggy年M月d日(ddd)
        /// ggy年M月d日
        /// yyyy年MM月dd日(ddd)
        /// yyyy年MM月dd日
        /// yyyy年M月d日(ddd)
        /// yyyy年M月d日
        /// MM月dd日(ddd)
        /// MM月dd日
        /// M月d日(ddd)
        /// M月d日
        /// yyyy/MM/dd(ddd)
        /// yyyy/MM/dd
        /// yyyy/M/d(ddd)
        /// yyyy/M/d
        /// MM/dd/(ddd)
        /// MM/dd
        /// M/d(ddd)
        /// M/d
        /// </summary>
        public static readonly DependencyProperty CustomDateFormatProperty =
                    DependencyProperty.Register("CustomDateFormat",
                            typeof(string),
                            typeof(DatePickerBehavior),
                            new FrameworkPropertyMetadata(null));
        public string CustomDateFormat
        {
            get { return (string)GetValue(CustomDateFormatProperty); }
            set { SetValue(CustomDateFormatProperty, value); }
        }
        #endregion
        #endregion

        #region OnAttached
        /// <summary>
        /// コンストラクタで、イベントへメソッドを登録する。
        /// </summary>
        protected override void OnAttached()
        {
            base.OnAttached();
            jpCalendar = new JapaneseCalendar();
            cultureInfo = new CultureInfo("ja-JP");
            cultureInfo.DateTimeFormat.Calendar = jpCalendar;

            AssociatedObject.Loaded += AssociatedObjectLoaded;
            AssociatedObject.SelectedDateChanged += AssociatedObjectSelectedDateChanged;
            AssociatedObject.PreviewKeyDown += OnPreviewKeyDown;
        }
        #endregion

        #region OnDetaching
        protected override void OnDetaching()
        {
            AssociatedObject.Loaded -= AssociatedObjectLoaded;
            AssociatedObject.SelectedDateChanged -= AssociatedObjectSelectedDateChanged;
            AssociatedObject.PreviewKeyDown -= OnPreviewKeyDown;
        }
        #endregion


        private void AssociatedObjectLoaded(object sender, RoutedEventArgs e)
        {
            DatePicker datePicker = sender as DatePicker;
            if (datePicker.Template == null)
            {
                return;
            }


            _textBox = datePicker.Template.FindName("PART_TextBox", datePicker) as DatePickerTextBox;
            if (_textBox != null)
            {
                //IME=OFF
                InputMethod.SetPreferredImeState(_textBox, InputMethodState.Off);

                _textBox.RemoveHandler(TextBox.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(TextBox_GotKeyboardFocus));
                _textBox.RemoveHandler(TextBox.LostFocusEvent, new RoutedEventHandler(TextBox_LostFocus));
                _textBox.RemoveHandler(TextBox.KeyDownEvent, new KeyEventHandler(TextBox_KeyDown));
                _textBox.RemoveHandler(TextBox.TextChangedEvent, new TextChangedEventHandler(TextBox_TextChanged));

                _textBox.AddHandler(TextBox.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(TextBox_GotKeyboardFocus));
                _textBox.AddHandler(TextBox.LostFocusEvent, new RoutedEventHandler(TextBox_LostFocus), true);
                _textBox.AddHandler(TextBox.KeyDownEvent, new KeyEventHandler(TextBox_KeyDown), true);
                _textBox.AddHandler(TextBox.TextChangedEvent, new TextChangedEventHandler(TextBox_TextChanged));

                // 上のコードでtrueにしている理由
                // EventSetter.HandledEventsToo プロパティ
                // https://docs.microsoft.com/ja-jp/dotnet/api/system.windows.eventsetter.handledeventstoo?view=net-5.0
                // イベント データ内でイベントが処理済みとしてマークされている場合でも、setter に割り当てられたハンドラーを呼び出す必要があるかどうかを判断する値を取得または設定します。
                // ハンドラーを引き続き呼び出す必要がある場合は true。それ以外の場合は false
            }

            // PART_Button
            // 年月のみでカレンダーを表示させる場合に使用します
            _calendarButton = datePicker.Template.FindName("PART_Button", datePicker) as Button;
            if (_calendarButton != null && IsMonthYear)
            {
                datePicker.CalendarOpened += DatePickerOnCalendarOpened;
                datePicker.CalendarClosed += DatePickerOnCalendarClosed;
            }

            // PART_TextBox
            // 表示用と編集用で表示フォーマットを変更します
            this.IscustomizeFormat = !string.IsNullOrEmpty(this.CustomDateFormat);
            if (this.IscustomizeFormat)
            {
                _textBox = datePicker.Template.FindName("PART_TextBox", datePicker) as DatePickerTextBox;

                if (_textBox != null && datePicker.SelectedDate != null)
                {

                    if (!_textBox.IsFocused || _textBox.IsReadOnly)
                    {
                        //フォーカスがあたっていない、もしくは読み取り専用の時はカスタム書式で表示
                        if (this.CustomDateFormat.StartsWith("g", StringComparison.CurrentCulture))
                        {
                            try
                            {
                                _textBox.Text = datePicker.SelectedDate.Value.ToString(this.CustomDateFormat, cultureInfo);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine(ex.Message);
                                // 明治より前はエラーになります
                                _textBox.Text = AssociatedObject.SelectedDate.Value.ToString("yyyy/MM/dd");
                            }
                        }
                        else
                        {
                            _textBox.Text = datePicker.SelectedDate.Value.ToString(this.CustomDateFormat, CultureInfo.CurrentCulture);
                        }
                    }
                    else if (!_textBox.IsReadOnly)
                    {
                        //フォーカスが合っていて、編集可能なときは、編集用の書式にする。
                        _textBox.Text = this.ToEditingDateFormat(datePicker.SelectedDate.Value, datePicker);
                    }
                }
            }
        }

        /// <summary>
        /// カレンダーを年月表示で表示させる
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DatePickerOnCalendarOpened(object sender, RoutedEventArgs e)
        {
            var calendar = GetDatePickerCalendar(sender);
            calendar.DisplayMode = CalendarMode.Year;

            calendar.DisplayModeChanged += CalendarOnDisplayModeChanged;
        }

        /// <summary>
        /// IsMonth=trueのときのカレンダーが閉じたときの動作
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="routedEventArgs"></param>
        private void DatePickerOnCalendarClosed(object sender, RoutedEventArgs routedEventArgs)
        {
            var calendar = GetDatePickerCalendar(sender);
            AssociatedObject.SelectedDate = calendar.SelectedDate;

            calendar.DisplayModeChanged -= CalendarOnDisplayModeChanged;
        }

        /// <summary>
        /// PART_Popup　カレンダーを取得
        /// </summary>
        /// <param name="sender"></param>
        /// <returns></returns>
        private System.Windows.Controls.Calendar GetDatePickerCalendar(object sender)
        {
            DatePicker datePicker = sender as DatePicker;
            var popup = (Popup)datePicker.Template.FindName("PART_Popup", datePicker);
            return ((System.Windows.Controls.Calendar)popup.Child);
        }

        private void CalendarOnDisplayModeChanged(object sender, CalendarModeChangedEventArgs e)
        {
            var calendar = (System.Windows.Controls.Calendar)sender;

            if (calendar.DisplayMode != CalendarMode.Month)
                return;

            calendar.SelectedDate = GetSelectedCalendarDate(calendar.DisplayDate);

            AssociatedObject.IsDropDownOpen = false;
        }

        private DateTime? GetSelectedCalendarDate(DateTime? selectedDate)
        {
            if (!selectedDate.HasValue)
                return null;
            return new DateTime(selectedDate.Value.Year, selectedDate.Value.Month, 1);
        }

        /// <summary>
        /// 編集用の書式を使って、文字列に変換します。
        /// </summary>
        /// <param name="date">日付</param>
        /// <returns>変換後の文字列</returns>
        private string ToEditingDateFormat(DateTime date, DatePicker datePicker)
        {
            if (datePicker.SelectedDateFormat == DatePickerFormat.Short)
            {
                //ShortDateString。
                return date.ToShortDateString();
            }
            else
            {
                //LongDateString
                return date.ToLongDateString();
            }
        }

        #region LostFocusイベント
        /// <summary>
        /// LostFocusしたら指定された表示用の書式にします
        /// </summary>
        /// <param name="sender">イベントソース</param>
        /// <param name="e">イベントデータ</param>
        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            SetTextBoxDate(sender);
        }

        private void SetTextBoxDate(object sender)
        {

            if (AssociatedObject.Template == null || !this.IscustomizeFormat)
            {
                return;
            }
            _textBox = sender as DatePickerTextBox;
            if (_textBox != null && AssociatedObject.SelectedDate != null)
            {
                //変換前に値を一時保持
                TempText = AssociatedObject.SelectedDate.Value.ToString();
                //フォーカス喪失時の書式設定に変換
                if (this.CustomDateFormat.StartsWith("g"))
                {
                    try
                    {
                        ViewText = AssociatedObject.SelectedDate.Value.ToString(this.CustomDateFormat, cultureInfo);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                        // 明治より前はエラーになります
                        ViewText = AssociatedObject.SelectedDate.Value.ToString("yyyy/MM/dd");
                    }
                }
                else
                {
                    ViewText = AssociatedObject.SelectedDate.Value.ToString(this.CustomDateFormat, CultureInfo.CurrentCulture);
                }

                _textBox.Text = ViewText;
                TempText = "";
            }
            else
            {
                TempText = "";
                ViewText = "";
            }
            return;
        }
        #endregion

        /// <summary>
        /// Enterキーで次のフォーカスに移動します
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {

            if (e.Key == Key.Enter & (sender as TextBox).AcceptsReturn == false && IsKeyEnterToNextControl == true)
            {
                KeyEventArgs e1 = new KeyEventArgs(
                                    e.KeyboardDevice,
                                    e.InputSource,
                                    e.Timestamp,
                                    Key.Tab);
                e1.RoutedEvent = Keyboard.KeyDownEvent;
                InputManager.Current.ProcessInput(e1);
            }
        }

        /// <summary>
        /// Key.DownでIsDropDownOpenをします
        /// </summary>
        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            Key key = e.Key;

            if (key == Key.System)
            {
                key = e.SystemKey;
            }

            if (key == Key.Down)
            {
                if (AssociatedObject.IsDropDownOpen == false)
                {
                    AssociatedObject.IsDropDownOpen = true;
                    e.Handled = true;
                    return;
                }
            }
        }

        /// <summary>
        /// キーボードフォーカス取得時は編集用のLong/Shortの書式に変更します
        /// </summary>
        /// <param name="sender">DatePickerTextBox</param>
        /// <param name="e"></param>
        private void TextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (!this.IscustomizeFormat)
            {
                return;
            }

            var textBox = sender as DatePickerTextBox;
            DateTime dateTime;

            //一時保持した日付を表示します  
            if (AssociatedObject.SelectedDate == null)
            {
                TempText = "";
                ViewText = "";
                return;
            }

            if (DateTime.TryParse(AssociatedObject.SelectedDate.Value.ToString(), out dateTime))
            {
                textBox.Text = this.ToEditingDateFormat(dateTime, AssociatedObject);
            }
            else
            {
                if (DateTime.TryParse(textBox.Text, out dateTime))
                {
                    textBox.Text = this.ToEditingDateFormat(dateTime, AssociatedObject);
                    TempText = textBox.Text;
                }
            }
            return;
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            this.SetValueNoCallback(DatePicker.TextProperty, this._textBox.Text);
        }

        private void SetValueNoCallback(DependencyProperty property, object value)
        {
            SetIsHandlerSuspended(property, true);
            try
            {
                SetCurrentValue(property, value);
            }
            finally
            {
                SetIsHandlerSuspended(property, false);
            }
        }

        private void SetIsHandlerSuspended(DependencyProperty property, bool value)
        {
            if (value)
            {
                if (_isHandlerSuspended == null)
                {
                    _isHandlerSuspended = new Dictionary<DependencyProperty, bool>(2);
                }

                _isHandlerSuspended[property] = true;
            }
            else
            {
                if (_isHandlerSuspended != null)
                {
                    _isHandlerSuspended.Remove(property);
                }
            }
        }


        /// <summary>
        /// 選択日付変更イベント用メソッド
        /// </summary>
        /// <param name="sender">イベントソース</param>
        /// <param name="e">イベントデータ</param>
        private void AssociatedObjectSelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {

            DatePicker datePicker = sender as DatePicker;

            if (datePicker.SelectedDate == null)
            {
                //PART_TextBoxの値がない場合は、カレンダーの初期値を本日にします
                datePicker.DisplayDate = DateTime.Now;
                return;
            }

            if (datePicker.Template == null)
                return;

            //テンプレート内のテキストボックスを検索します。
            var dateTextBox = datePicker.Template.FindName("PART_TextBox", datePicker) as DatePickerTextBox;

            if (dateTextBox != null)
            {
                if (string.IsNullOrWhiteSpace(dateTextBox.Text))
                    return;

                //変換前に値を一時保持
                TempText = datePicker.SelectedDate.Value.ToString();

                //編集中の書式
                if (datePicker.IsFocused || dateTextBox.IsFocused)
                {
                    dateTextBox.Text = this.ToEditingDateFormat(datePicker.SelectedDate.Value, datePicker);
                }
                else
                {
                    if (this.IscustomizeFormat)
                    {
                        if (this.CustomDateFormat.StartsWith("g"))
                        {
                            try
                            {
                                dateTextBox.Text = datePicker.SelectedDate.Value.ToString(this.CustomDateFormat, cultureInfo);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine(ex.Message);
                                // 明治より前はエラーになります
                                dateTextBox.Text = datePicker.SelectedDate.Value.ToString("yyyy/MM/dd");
                            }
                        }
                        else
                        {
                            dateTextBox.Text = datePicker.SelectedDate.Value.ToString(this.CustomDateFormat, CultureInfo.CurrentCulture);
                        }
                    }
                    else
                    {
                        dateTextBox.Text = datePicker.SelectedDate.Value.ToString("yyyy/MM/dd");
                    }
                }
            }
        }

    }
}
