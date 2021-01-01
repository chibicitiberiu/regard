using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Frontend.Shared.Controls
{
    public enum DialogResult
    {
        Dismissed,
        Primary,
        Secondary
    }

    public partial class Dialog
    {
        private Func<DialogResult, Task> resultCallback;
        private Modal modal;

        [Parameter]
        public string Caption { get; set; }

        [Parameter]
        public RenderFragment ChildContent { get; set; }

        [Parameter]
        public string Text { get; set; }

        [Parameter]
        public string PrimaryButtonCaption { get; set; }

        [Parameter]
        public string SecondaryButtonCaption { get; set; }

        public async Task ShowDialog(string caption, string text, string primaryButtonCaption, string secondaryButtonCaption, Func<DialogResult, Task> resultCallback)
        {
            this.Caption = caption;
            this.Text = text;
            this.PrimaryButtonCaption = primaryButtonCaption;
            this.SecondaryButtonCaption = secondaryButtonCaption;
            this.resultCallback = resultCallback;
            await modal.Show();
        }

        public async Task ShowDialog(string caption, Func<DialogResult, Task> resultCallback)
        {
            this.Caption = caption;
            this.resultCallback = resultCallback;
            await modal.Show();
        }

        public async Task ShowDialog(Func<DialogResult, Task> resultCallback)
        {
            this.resultCallback = resultCallback;
            await modal.Show();
        }

        private async Task OnPrimaryClicked()
        {
            if (resultCallback != null)
                await resultCallback.Invoke(DialogResult.Primary);
            resultCallback = null;
            await modal.Close();
        }

        private async Task OnSecondaryClicked()
        {
            if (resultCallback != null)
                await resultCallback.Invoke(DialogResult.Secondary);
            resultCallback = null;
            await modal.Close();
        }

        private async Task OnClosed()
        {
            if (resultCallback != null)
                await resultCallback.Invoke(DialogResult.Dismissed);
            resultCallback = null;
        }
    }
}
