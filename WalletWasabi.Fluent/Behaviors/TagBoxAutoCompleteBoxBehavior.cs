using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Input;
using Avalonia.Threading;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors
{
    public class TagBoxAutoCompleteBoxBehavior : Behavior<AutoCompleteBox>
    {
        public static readonly StyledProperty<Action<string>> CommitTextActionProperty =
            AvaloniaProperty.Register<SplitViewAutoBehavior, Action<string>>(nameof(CommitTextAction));

        public static readonly StyledProperty<Action> BackspaceAndEmptyTextActionProperty =
            AvaloniaProperty.Register<SplitViewAutoBehavior, Action>(nameof(BackspaceAndEmptyTextAction));

        public Action<string> CommitTextAction
        {
            get => GetValue(CommitTextActionProperty);
            set => SetValue(CommitTextActionProperty, value);
        }

        public Action BackspaceAndEmptyTextAction
        {
            get => GetValue(BackspaceAndEmptyTextActionProperty);
            set => SetValue(BackspaceAndEmptyTextActionProperty, value);
        }

        protected override void OnAttached()
        {
            AssociatedObject.KeyUp += OnKeyUp;
            AssociatedObject.TextChanged += OnTextChanged;
            AssociatedObject.DropDownClosed += OnDropDownClosed;

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Refocus because the old control is destroyed
                // when the tag list changes.
                AssociatedObject.Focus();
            });

            base.OnAttached();
        }

        private void OnDropDownClosed(object? sender, EventArgs e)
        {
            var currentText = AssociatedObject?.Text ?? "";
            var selItem = AssociatedObject?.SelectedItem as string;

            if (currentText.Length == 0)
            {
                return;
            }

            if (selItem is null || selItem.Length == 0) return;

            CommitTextAction?.Invoke(AssociatedObject?.Text?.Trim() ?? "");
            AssociatedObject?.ClearValue(AutoCompleteBox.SelectedItemProperty);

            Dispatcher.UIThread.Post(() =>
            {
                AssociatedObject?.ClearValue(AutoCompleteBox.TextProperty);
            });
        }

        private void OnTextChanged(object? sender, EventArgs e)
        {
            var currentText = AssociatedObject?.Text ?? "";

            if (currentText.Length >= 1 && !string.IsNullOrEmpty(currentText.Trim()) && currentText.EndsWith(' '))
            {
                CommitTextAction?.Invoke(AssociatedObject?.Text?.Trim() ?? "");
                Dispatcher.UIThread.Post(() => { AssociatedObject?.ClearValue(AutoCompleteBox.TextProperty); });
            }
        }

        private void OnKeyUp(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Back && (AssociatedObject?.Text?.Length == 0 || AssociatedObject?.Text is null))
            {
                BackspaceAndEmptyTextAction?.Invoke();
            }
            else if (e.Key == Key.Enter && !string.IsNullOrEmpty(AssociatedObject?.Text?.Trim() ?? ""))
            {
                CommitTextAction?.Invoke(AssociatedObject?.Text?.Trim() ?? "");
                Dispatcher.UIThread.Post(() => { AssociatedObject?.ClearValue(AutoCompleteBox.TextProperty); });
            }
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
            AssociatedObject.DropDownClosed -= OnDropDownClosed;
            AssociatedObject.KeyUp -= OnKeyUp;
            AssociatedObject.TextChanged -= OnTextChanged;
        }
    }
}