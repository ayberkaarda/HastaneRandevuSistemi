using HastaneRandevu.Domain.Entities;
using HastaneRandevu.UI.Infrastructure;
using HastaneRandevu.UI.Tests.Support;
using HastaneRandevu.UI.Theming;
using static HastaneRandevu.UI.Tests.Support.StaThread;

namespace HastaneRandevu.UI.Tests;

/// <summary>
/// Every screen is built with test doubles and laid out without being shown: window handles are created
/// for real layout and tab order, but no window becomes visible and no message window opens.
/// </summary>
public sealed class ScreenTests
{
    private static readonly DoctorSummary Ada = new(1, "Dr. Ada Örnek", "Kardiyoloji");

    public static TheoryData<string> ScreenNames =>
    [
        nameof(LoginForm), nameof(UyeOlForm), nameof(DoktorGirisForm), nameof(MainForm),
        nameof(TakvimForm), nameof(ErteleForm), nameof(DoktorForm), nameof(CalismaProgramiForm),
    ];

    [Theory]
    [MemberData(nameof(ScreenNames))]
    public void Screen_HasTheApplicationIdentity(string name) => Run(() =>
    {
        using var form = Create(name);

        Assert.StartsWith(AppText.Title + " - ", form.Text, StringComparison.Ordinal);
        Assert.Same(AppAssets.Icon, form.Icon);
        Assert.Equal(FormBorderStyle.Sizable, form.FormBorderStyle);
        Assert.True(form.MinimumSize.Width > 0 && form.MinimumSize.Height > 0, "A resizable screen needs a minimum size.");
        Assert.Equal(AutoScaleMode.Dpi, form.AutoScaleMode);
        Assert.Null(form.BackgroundImage);
        Assert.False(form.Visible);
    });

    [Theory]
    [MemberData(nameof(ScreenNames))]
    public void Screen_UsesThemedControlsAndAKeyboardWayOut(string name) => Run(() =>
    {
        using var form = Create(name);
        var controls = Descendants(form).ToList();

        var buttons = controls.OfType<Button>().ToList();
        Assert.NotEmpty(buttons);
        Assert.All(buttons, button => Assert.True(button is ThemedButton or DayButton, $"{button.Name} is not themed."));
        Assert.All(buttons.Where(b => b is not DayButton), button => Assert.False(string.IsNullOrWhiteSpace(button.Text), $"{button.Name} has no text."));
        Assert.All(controls.OfType<ComboBox>(), combo => Assert.IsType<ThemedComboBox>(combo));
        Assert.All(controls.OfType<ListBox>(), list => Assert.IsType<ThemedListBox>(list));
        Assert.All(controls.OfType<CheckBox>(), check => Assert.IsType<ThemedCheckBox>(check));
        Assert.All(controls.OfType<RadioButton>(), radio => Assert.IsType<SegmentButton>(radio));
        Assert.All(controls.OfType<DateTimePicker>(), picker => Assert.IsType<DateField>(picker));
        Assert.All(controls.OfType<TextBox>(), box => Assert.IsType<TextField>(box.Parent));
        Assert.All(controls.OfType<ListBox>(), list => Assert.False(string.IsNullOrEmpty(((ThemedListBox)list).EmptyText), $"{list.Name} has no empty text."));
        Assert.NotNull(form.CancelButton); // Esc closes every screen.
    });

    [Theory]
    [MemberData(nameof(ScreenNames))]
    public void Screen_LaysOutInsideItsWindow(string name) => Run(() =>
    {
        using var form = Create(name);
        CreateHandles(form);
        form.PerformLayout();

        var overflow = Descendants(form)
            .Where(control => control.Parent is not ScrollableControl { AutoScroll: true }) // A scrolling area may be taller.
            .Where(control => !IsInsideParent(control))
            .Select(control => $"{control.Name} ({control.GetType().Name}) {control.Bounds} in {control.Parent!.Name} {control.Parent.ClientRectangle}")
            .ToList();

        Assert.Empty(overflow);
        Assert.Empty(Overlaps(form));
        Assert.False(form.Visible);
    });

    public static TheoryData<string, int, int, float> ScreensOnSmallScreens
    {
        get
        {
            var data = new TheoryData<string, int, int, float>();
            foreach (var (width, height) in new[] { (1024, 600), (1366, 768) })
            {
                foreach (var scale in new[] { 1F, 1.25F, 1.5F, 2F })
                {
                    foreach (var name in ScreenNames)
                    {
                        data.Add(name, width, height, scale);
                    }
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(ScreensOnSmallScreens))]
    public void Screen_FitsTheWorkingAreaAndKeepsEveryControlReachable(string name, int screenWidth, int screenHeight, float fontScale) => Run(() =>
    {
        // Larger fonts stand in for a different font, DPI or text scale; the working area for a small screen.
        var previous = Theme.Current;
        var previousScale = Theme.RenderScale;
        var previousArea = AppDialog.WorkingAreaOf;
        Theme.Use(previous, fontScale);
        var area = new Rectangle(0, 0, screenWidth, screenHeight);
        AppDialog.WorkingAreaOf = _ => area;
        try
        {
            using var form = (AppDialog)Create(name);
            CreateHandles(form);

            Assert.True(form.Width <= area.Width && form.Height <= area.Height, $"{name} opens at {form.Size}, larger than the working area {area.Size}.");
            Assert.True(form.MinimumSize.Width <= area.Width && form.MinimumSize.Height <= area.Height, $"{name} requires {form.MinimumSize}, larger than the working area {area.Size}.");

            foreach (var size in new[] { form.Size, form.MinimumSize })
            {
                form.Size = size;
                form.ShowStatus(StatusKind.Warning, "Lütfen bir randevu saati seçiniz." + Environment.NewLine + "İkinci satır.");
                form.PerformLayout();

                var body = Find<ScrollableControl>(form, "body");
                var content = Find<Panel>(form, "content");
                var unreachable = Descendants(form)
                    .Where(control => control is not (Label or Panel or TableLayoutPanel or FlowLayoutPanel or InfoBar or GlyphBadge or BrandRule or Swatch))
                    .Where(control => control is not TextBox { Parent: TextField }) // Judged through the frame.
                    .Select(control => (Control: control, Problem: Problem(form, body, content, control)))
                    .Where(item => item.Problem is not null)
                    .Select(item => $"{item.Control.Name} ({item.Control.GetType().Name}): {item.Problem}")
                    .ToList();

                Assert.True(unreachable.Count == 0, $"{name} at {form.Size}: " + string.Join(" || ", unreachable));
                Assert.Empty(Overlaps(form));
                form.ClearStatus();
            }
        }
        finally
        {
            AppDialog.WorkingAreaOf = previousArea;
            Theme.Use(previous, previousScale);
        }
    });

    [Theory]
    [InlineData(nameof(LoginForm), "textBoxTC", "buttonCikis")]
    [InlineData(nameof(UyeOlForm), "textBoxIsim", "buttonKapat")]
    [InlineData(nameof(DoktorGirisForm), "textBoxDoktorIsim", "buttonIptal")]
    [InlineData(nameof(MainForm), "textBoxDoktorAra", "buttonCikis")]
    [InlineData(nameof(TakvimForm), "radioButtonAy", "buttonKapat")]
    [InlineData(nameof(ErteleForm), "dateTimePickerYeni", "buttonVazgec")]
    [InlineData(nameof(DoktorForm), "listBoxRandevular", "buttonCikis")]
    [InlineData(nameof(CalismaProgramiForm), "checkBoxMonday", "buttonKapat")]
    public void TabOrder_StartsAtTheFirstInputAndEndsInTheFooter(string name, string first, string last) => Run(() =>
    {
        using var form = Create(name);
        CreateHandles(form);

        var order = TabOrder(form);

        Assert.Equal(first, order[0].Name);
        Assert.Equal(last, order[^1].Name);
        Assert.Equal(order.Count, order.Distinct().Count());
    });

    [Theory]
    [InlineData(nameof(LoginForm), new[] { "textBoxTC", "textBoxSifre", "buttonGiris", "buttonUyeOl", "buttonDoktorGiris", "buttonCikis" })]
    [InlineData(nameof(UyeOlForm), new[] { "textBoxIsim", "textBoxTC", "textBoxSifre", "buttonKaydet", "buttonKapat" })]
    [InlineData(nameof(MainForm), new[]
    {
        "textBoxDoktorAra", "comboBoxBrans", "comboBoxDoktorlar", "dateTimePickerRandevu", "comboBoxSaat", "buttonIlkBos", "buttonTakvim",
        "buttonRandevuAl", "listBoxRandevuGecmisi", "buttonErtele", "buttonRandevuIptal", "buttonGecmisCsv", "buttonCikis",
    })]
    [InlineData(nameof(TakvimForm), new[] { "radioButtonAy", "buttonOnceki", "buttonSonraki", "buttonKapat" })] // Arrow keys move inside the month/week group.
    [InlineData(nameof(ErteleForm), new[] { "dateTimePickerYeni", "comboBoxYeniSaat", "buttonErtele", "buttonVazgec" })]
    [InlineData(nameof(DoktorForm), new[] { "listBoxRandevular", "buttonRandevuIptal", "dateTimePickerAjanda", "buttonAjanda", "buttonCalismaProgrami", "buttonCikis" })]
    [InlineData(nameof(CalismaProgramiForm), new[]
    {
        "checkBoxMonday", "checkBoxSunday", "buttonKaydet", "dateTimePickerIzin", "buttonIzinEkle", "listBoxIzinler", "buttonIzinKaldir",
        "listBoxCakisanlar", "buttonKapat",
    })]
    public void TabOrder_FollowsTheReadingOrder(string name, string[] expected) => Run(() =>
    {
        using var form = Create(name);
        CreateHandles(form);

        var names = TabOrder(form).Select(control => control.Name).Where(expected.Contains).ToList();

        Assert.Equal(expected, names);
    });

    [Fact]
    public void FieldCaptions_NameTheirInputs() => Run(() =>
    {
        using var form = new LoginForm(new PendingAuthService(), new NoFormsFactory());

        Assert.Equal("T.C. Kimlik No", Find<TextBox>(form, "textBoxTC").AccessibleName);
        Assert.Equal("Şifre", Find<TextBox>(form, "textBoxSifre").AccessibleName);
        Assert.False(Descendants(form).OfType<TextField>().Any(field => field.CanSelect), "A text field frame must hand the focus to its text box.");
    });

    [Fact]
    public void Validation_IsShownInlineWithoutADialog() => Run(() =>
    {
        using var form = TestForms.PatientPanel(new ControlledAppointmentService());
        var dialogs = NoDialogs.Requested.Count;

        Assert.True(form.BookAsync().IsCompletedSuccessfully);

        Assert.True(form.StatusBar.IsShown);
        Assert.Equal(StatusKind.Warning, form.StatusBar.Kind);
        Assert.Equal("Lütfen bir doktor seçiniz.", form.StatusBar.Text);
        Assert.Equal(form.StatusBar.Text, form.StatusBar.AccessibleName);
        Assert.Equal(dialogs, NoDialogs.Requested.Count);
    });

    [Fact]
    public void NextWrite_ClearsThePreviousMessage() => Run(() =>
    {
        var appointments = new ControlledAppointmentService();
        using var form = TestForms.PatientPanel(appointments);
        Assert.True(form.BookAsync().IsCompletedSuccessfully);
        Assert.True(form.StatusBar.IsShown);
        var doctors = Find<ComboBox>(form, "comboBoxDoktorlar");
        doctors.Items.Add(appointments.Doctors[0]);
        doctors.SelectedIndex = 0;
        var slots = Find<ComboBox>(form, "comboBoxSaat");
        slots.Items.Add(new SlotItem(DateTime.Today.AddDays(1).AddHours(10)));
        slots.SelectedIndex = 0;

        _ = form.BookAsync(); // Never completes; the old warning must be gone meanwhile.

        Assert.Equal(1, appointments.BookCalls);
        Assert.False(form.StatusBar.IsShown);
    });

    [Fact]
    public void CancelQuestion_GoesThroughTheDialogHelper() => Run(() =>
    {
        var appointments = new ControlledAppointmentService();
        using var form = TestForms.DoctorPanel(appointments);
        var list = Find<ListBox>(form, "listBoxRandevular");
        list.Items.Add(new DoctorAppointmentItem(Sessions.Appointment(5)));
        list.SelectedIndex = 0;
        var before = NoDialogs.Requested.Count;

        Assert.True(form.CancelAsync().IsCompletedSuccessfully);

        Assert.Equal("Onay: Seçilen randevuyu iptal etmek istediğinize emin misiniz?", Assert.Single(NoDialogs.Requested.Skip(before)));
        Assert.Equal(0, appointments.CancelCalls); // The fixture answers "No".
    });

    [Fact]
    public void ResultOfAClosingDialog_IsShownInTheWindowThatOpenedIt() => Run(() =>
    {
        using var panel = TestForms.PatientPanel(new ControlledAppointmentService());
        using var dialog = new ErteleForm(new ControlledAppointmentService(), Sessions.Patient, Sessions.Appointment(5));
        var prompts = new ScreenPrompts(dialog) { Results = panel };

        prompts.Inform("Randevunuz ertelendi.");
        prompts.Warn("Lütfen yeni bir randevu saati seçiniz.");

        Assert.Equal((StatusKind.Success, "Randevunuz ertelendi."), (panel.StatusBar.Kind, panel.StatusBar.Text));
        Assert.Equal((StatusKind.Warning, "Lütfen yeni bir randevu saati seçiniz."), (dialog.StatusBar.Kind, dialog.StatusBar.Text));
    });

    [Fact]
    public void SlotPicker_SaysWhenItLoadsAndWhenTheDayIsFull() => Run(() =>
    {
        var appointments = new ControlledAppointmentService();
        using var form = TestForms.PatientPanel(appointments);
        var doctors = Find<ComboBox>(form, "comboBoxDoktorlar");
        var slots = Find<ThemedComboBox>(form, "comboBoxSaat");
        doctors.Items.Add(appointments.Doctors[0]);

        doctors.SelectedIndex = 0;
        Assert.Equal("Yükleniyor…", slots.Placeholder);

        appointments.SlotRequests[0].Answer.SetResult([]);
        Assert.Equal("Bu gün boş saat yok", slots.Placeholder);
        Assert.Null(slots.SelectedItem);
    });

    [Fact]
    public void EmptyLists_ExplainThemselvesAndStopLoading() => Run(() =>
    {
        using var form = TestForms.DoctorPanel(new ControlledAppointmentService());

        Assert.True(form.LoadAsync().IsCompletedSuccessfully);

        var list = Find<ThemedListBox>(form, "listBoxRandevular");
        Assert.Empty(list.Items);
        Assert.False(list.IsLoading);
        Assert.Equal("Sizinle alınmış aktif bir randevu yok.", list.EmptyText);
    });

    [Fact]
    public void NoReminder_LeavesTheTextEmptyAndDescribesTheQuietState() => Run(() =>
    {
        using var form = TestForms.PatientPanel(new ControlledAppointmentService());

        Assert.True(form.LoadAsync().IsCompletedSuccessfully);

        var reminder = Assert.IsType<PlaceholderLabel>(Find<Label>(form, "labelHatirlatma"));
        Assert.Equal(string.Empty, reminder.Text);
        Assert.Equal(reminder.Placeholder, reminder.AccessibleDescription);
    });

    [Fact]
    public void Calendar_TilesKeepTheirTextAndDimOtherMonths() => Run(() =>
    {
        var availability = new ControlledAvailabilityService();
        using var form = new TakvimForm(availability, Sessions.Patient, Ada);

        _ = form.ShowPeriodAsync();
        var request = availability.Calendars[0];
        availability.Calendars[0].Answer.SetResult(
            [.. Enumerable.Range(0, request.Days).Select(i => new Business.Services.CalendarDay(request.FirstDay.AddDays(i), Business.Services.CalendarDayStatus.Available, 3, 16))]);

        var first = Find<DayButton>(form, "buttonGun0");
        var inMonth = Enumerable.Range(0, 42).Select(i => Find<DayButton>(form, "buttonGun" + i)).First(b => ((Business.Services.CalendarDay)b.Tag!).Date.Day == 1);
        Assert.EndsWith("Boş: 3/16", first.Text, StringComparison.Ordinal);
        Assert.True(inMonth.InPeriod);
        Assert.Equal(request.FirstDay.Day == 1, first.InPeriod);
        Assert.Equal("3/16 boş", DayButton.Detail((Business.Services.CalendarDay)first.Tag!));
    });

    [Fact]
    public void MessageDialog_AnswersWithKeysAndIsNeverShown() => Run(() =>
    {
        using var dialog = new MessageDialog("Seçilen randevuyu iptal etmek istediğinize emin misiniz?", "Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        Assert.False(dialog.Visible);
        Assert.Equal(DialogResult.Yes, Assert.IsType<ThemedButton>(dialog.AcceptButton).DialogResult);
        Assert.Equal(DialogResult.No, Assert.IsType<ThemedButton>(dialog.CancelButton).DialogResult);
        Assert.Equal(AppText.Title, dialog.Text);
        Assert.True(dialog.ClientSize.Width > 0 && dialog.ClientSize.Height > 0);
        Assert.Equal("Bir sorun oluştu", MessageDialog.Heading(AppText.Title, MessageBoxIcon.Error));
        Assert.Equal("Uyarı", MessageDialog.Heading("Uyarı", MessageBoxIcon.Warning));
    });

    private static Form Create(string name) => name switch
    {
        nameof(LoginForm) => new LoginForm(new PendingAuthService(), new NoFormsFactory()),
        nameof(UyeOlForm) => new UyeOlForm(new PendingAuthService()),
        nameof(DoktorGirisForm) => new DoktorGirisForm(new PendingAuthService(), new NoFormsFactory()),
        nameof(MainForm) => TestForms.PatientPanel(new ControlledAppointmentService()),
        nameof(TakvimForm) => new TakvimForm(new ControlledAvailabilityService(), Sessions.Patient, Ada),
        nameof(ErteleForm) => new ErteleForm(new ControlledAppointmentService(), Sessions.Patient, Sessions.Appointment(5)),
        nameof(DoktorForm) => TestForms.DoctorPanel(new ControlledAppointmentService()),
        nameof(CalismaProgramiForm) => new CalismaProgramiForm(new ControlledScheduleService(), Sessions.Doctor),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
    };

    /// <summary>
    /// Why a control cannot be used: it is cut off by a container that does not scroll, or it lies outside the
    /// area the scrolling body can reach. Containers of zero size cut off everything they hold.
    /// </summary>
    private static string? Problem(Form form, ScrollableControl body, Panel content, Control control)
    {
        if (control.Width <= 0 || control.Height <= 0)
        {
            return null; // Never shown (an empty label or a day outside the week view).
        }

        var offset = control.Location;
        for (var ancestor = control.Parent; ancestor is not null && ancestor != form; ancestor = ancestor.Parent)
        {
            var client = ancestor == body || ancestor is ScrollableControl { AutoScroll: true }
                ? ancestor.DisplayRectangle.Size // Scrolling areas reach their whole display area.
                : ancestor.ClientSize;
            if (offset.X + control.Width > client.Width + 1 || offset.Y + control.Height > client.Height + 1 || offset.X < -1 || offset.Y < -1)
            {
                return $"cut off by {ancestor.Name} {ancestor.ClientSize}";
            }

            offset = new Point(offset.X + ancestor.Left, offset.Y + ancestor.Top);
        }

        if (!IsInside(body, control))
        {
            // Header, side panel and footer stay put: they must be fully inside the window.
            return form.ClientRectangle.Contains(form.RectangleToClient(control.RectangleToScreen(control.ClientRectangle))) ? null : "outside the window";
        }

        // Inside the body: reachable when the body scrolls over everything the content needs.
        var needed = content.Bounds;
        var scrollable = body.AutoScroll && body.DisplayRectangle.Height >= needed.Bottom && body.DisplayRectangle.Width >= needed.Right;
        return scrollable ? null : $"the body {body.ClientSize} does not scroll over {needed}";
    }

    /// <summary>Inputs and buttons that overlap a sibling; labels and containers may touch.</summary>
    private static List<string> Overlaps(Control root)
    {
        var problems = new List<string>();
        foreach (var parent in Descendants(root).Prepend(root))
        {
            var children = parent.Controls.Cast<Control>()
                .Where(c => c.Width > 0 && c.Height > 0 && c is not InfoBar)
                .ToList();
            for (var i = 0; i < children.Count; i++)
            {
                for (var j = i + 1; j < children.Count; j++)
                {
                    if (children[i].Bounds.IntersectsWith(children[j].Bounds))
                    {
                        problems.Add($"{children[i].Name} {children[i].Bounds} overlaps {children[j].Name} {children[j].Bounds} in {parent.Name}");
                    }
                }
            }
        }

        return problems;
    }

    private static bool IsInside(Control container, Control control)
    {
        for (var parent = control.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent == container)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInsideParent(Control control)
    {
        const int Tolerance = 1;
        var area = control.Parent!.ClientRectangle;
        area.Inflate(Tolerance, Tolerance);
        return control.Width == 0 || control.Height == 0 || area.Contains(control.Bounds);
    }

    /// <summary>
    /// Creates the window handles of a form that is never shown, so layout and tab order are real.
    /// The handles stay hidden: a control only becomes visible when its form is shown.
    /// </summary>
    private static void CreateHandles(Control control)
    {
        _ = control.Handle;
        foreach (Control child in control.Controls)
        {
            CreateHandles(child);
        }
    }

    private static IEnumerable<Control> Descendants(Control control)
    {
        foreach (Control child in control.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child))
            {
                yield return nested;
            }
        }
    }

    /// <summary>Focusable controls in the order Tab visits them.</summary>
    private static List<Control> TabOrder(Form form)
    {
        var order = new List<Control>();
        Control? current = null;
        while ((current = form.GetNextControl(current, forward: true)) is not null)
        {
            if (CanSelectWhenShown(current))
            {
                order.Add(current);
            }
        }

        return order;
    }

    /// <summary>
    /// Whether Tab can stop on the control once the window is shown: a tab stop that is enabled and not a
    /// container, label, frame or picture, nor a day of the calendar (they are enabled only once loaded).
    /// </summary>
    private static bool CanSelectWhenShown(Control control) =>
        control.TabStop
        && control.Enabled
        && control is not (Label or Panel or TableLayoutPanel or FlowLayoutPanel or TextField or GlyphBadge or InfoBar or BrandRule or Swatch);
}
