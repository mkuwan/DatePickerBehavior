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
        #region �����f�[�^
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
        /// Enter�L�[�Ńt�H�[�J�X�ړ�����t���O
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
        /// �J�����_�[��N���܂łƂ���t���O
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

        #region �a��p
        private System.Globalization.JapaneseCalendar jpCalendar;
        private System.Globalization.CultureInfo cultureInfo;
        #endregion

        #region �J�X�^������
        /// <summary>
        /// ����
        /// ggy�NMM��dd��(ddd)
        /// ggy�NMM��dd��
        /// ggy�NM��d��(ddd)
        /// ggy�NM��d��
        /// yyyy�NMM��dd��(ddd)
        /// yyyy�NMM��dd��
        /// yyyy�NM��d��(ddd)
        /// yyyy�NM��d��
        /// MM��dd��(ddd)
        /// MM��dd��
        /// M��d��(ddd)
        /// M��d��
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
        /// �R���X�g���N�^�ŁA�C�x���g�փ��\�b�h��o�^����B
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

                // ��̃R�[�h��true�ɂ��Ă��闝�R
                // EventSetter.HandledEventsToo �v���p�e�B
                // https://docs.microsoft.com/ja-jp/dotnet/api/system.windows.eventsetter.handledeventstoo?view=net-5.0
                // �C�x���g �f�[�^���ŃC�x���g�������ς݂Ƃ��ă}�[�N����Ă���ꍇ�ł��Asetter �Ɋ��蓖�Ă�ꂽ�n���h���[���Ăяo���K�v�����邩�ǂ����𔻒f����l���擾�܂��͐ݒ肵�܂��B
                // �n���h���[�����������Ăяo���K�v������ꍇ�� true�B����ȊO�̏ꍇ�� false
            }

            // PART_Button
            // �N���݂̂ŃJ�����_�[��\��������ꍇ�Ɏg�p���܂�
            _calendarButton = datePicker.Template.FindName("PART_Button", datePicker) as Button;
            if (_calendarButton != null && IsMonthYear)
            {
                datePicker.CalendarOpened += DatePickerOnCalendarOpened;
                datePicker.CalendarClosed += DatePickerOnCalendarClosed;
            }

            // PART_TextBox
            // �\���p�ƕҏW�p�ŕ\���t�H�[�}�b�g��ύX���܂�
            this.IscustomizeFormat = !string.IsNullOrEmpty(this.CustomDateFormat);
            if (this.IscustomizeFormat)
            {
                _textBox = datePicker.Template.FindName("PART_TextBox", datePicker) as DatePickerTextBox;

                if (_textBox != null && datePicker.SelectedDate != null)
                {

                    if (!_textBox.IsFocused || _textBox.IsReadOnly)
                    {
                        //�t�H�[�J�X���������Ă��Ȃ��A�������͓ǂݎ���p�̎��̓J�X�^�������ŕ\��
                        if (this.CustomDateFormat.StartsWith("g", StringComparison.CurrentCulture))
                        {
                            try
                            {
                                _textBox.Text = datePicker.SelectedDate.Value.ToString(this.CustomDateFormat, cultureInfo);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine(ex.Message);
                                // �������O�̓G���[�ɂȂ�܂�
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
                        //�t�H�[�J�X�������Ă��āA�ҏW�\�ȂƂ��́A�ҏW�p�̏����ɂ���B
                        _textBox.Text = this.ToEditingDateFormat(datePicker.SelectedDate.Value, datePicker);
                    }
                }
            }
        }

        /// <summary>
        /// �J�����_�[��N���\���ŕ\��������
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
        /// IsMonth=true�̂Ƃ��̃J�����_�[�������Ƃ��̓���
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
        /// PART_Popup�@�J�����_�[���擾
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
        /// �ҏW�p�̏������g���āA������ɕϊ����܂��B
        /// </summary>
        /// <param name="date">���t</param>
        /// <returns>�ϊ���̕�����</returns>
        private string ToEditingDateFormat(DateTime date, DatePicker datePicker)
        {
            if (datePicker.SelectedDateFormat == DatePickerFormat.Short)
            {
                //ShortDateString�B
                return date.ToShortDateString();
            }
            else
            {
                //LongDateString
                return date.ToLongDateString();
            }
        }

        #region LostFocus�C�x���g
        /// <summary>
        /// LostFocus������w�肳�ꂽ�\���p�̏����ɂ��܂�
        /// </summary>
        /// <param name="sender">�C�x���g�\�[�X</param>
        /// <param name="e">�C�x���g�f�[�^</param>
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
                //�ϊ��O�ɒl���ꎞ�ێ�
                TempText = AssociatedObject.SelectedDate.Value.ToString();
                //�t�H�[�J�X�r�����̏����ݒ�ɕϊ�
                if (this.CustomDateFormat.StartsWith("g"))
                {
                    try
                    {
                        ViewText = AssociatedObject.SelectedDate.Value.ToString(this.CustomDateFormat, cultureInfo);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                        // �������O�̓G���[�ɂȂ�܂�
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
        /// Enter�L�[�Ŏ��̃t�H�[�J�X�Ɉړ����܂�
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
        /// Key.Down��IsDropDownOpen�����܂�
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
        /// �L�[�{�[�h�t�H�[�J�X�擾���͕ҏW�p��Long/Short�̏����ɕύX���܂�
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

            //�ꎞ�ێ��������t��\�����܂�  
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
        /// �I����t�ύX�C�x���g�p���\�b�h
        /// </summary>
        /// <param name="sender">�C�x���g�\�[�X</param>
        /// <param name="e">�C�x���g�f�[�^</param>
        private void AssociatedObjectSelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {

            DatePicker datePicker = sender as DatePicker;

            if (datePicker.SelectedDate == null)
            {
                //PART_TextBox�̒l���Ȃ��ꍇ�́A�J�����_�[�̏����l��{���ɂ��܂�
                datePicker.DisplayDate = DateTime.Now;
                return;
            }

            if (datePicker.Template == null)
                return;

            //�e���v���[�g���̃e�L�X�g�{�b�N�X���������܂��B
            var dateTextBox = datePicker.Template.FindName("PART_TextBox", datePicker) as DatePickerTextBox;

            if (dateTextBox != null)
            {
                if (string.IsNullOrWhiteSpace(dateTextBox.Text))
                    return;

                //�ϊ��O�ɒl���ꎞ�ێ�
                TempText = datePicker.SelectedDate.Value.ToString();

                //�ҏW���̏���
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
                                // �������O�̓G���[�ɂȂ�܂�
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
