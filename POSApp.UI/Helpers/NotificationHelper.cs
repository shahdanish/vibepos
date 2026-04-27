using System.Windows;
using System.Media;

namespace POSApp.UI.Helpers
{
    /// <summary>
    /// Provides professional notification dialogs with consistent styling
    /// </summary>
    public static class NotificationHelper
    {
        public static void ShowSuccess(string message, string title = "Success")
        {
            SystemSounds.Asterisk.Play();
            MessageBox.Show(
                message,
                $"✅ {title}",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }

        public static void ShowError(string message, string title = "Error")
        {
            SystemSounds.Hand.Play();
            MessageBox.Show(
                message,
                $"❌ {title}",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }

        public static void ShowWarning(string message, string title = "Warning")
        {
            SystemSounds.Exclamation.Play();
            MessageBox.Show(
                message,
                $"⚠️ {title}",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }

        public static void ShowInfo(string message, string title = "Information")
        {
            SystemSounds.Asterisk.Play();
            MessageBox.Show(
                message,
                $"ℹ️ {title}",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }

        public static bool Confirm(string message, string title = "Confirm Action")
        {
            SystemSounds.Question.Play();
            var result = MessageBox.Show(
                message,
                $"❓ {title}",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No
            );
            return result == MessageBoxResult.Yes;
        }

        public static MessageBoxResult ConfirmWithCancel(string message, string title = "Confirm Action")
        {
            SystemSounds.Question.Play();
            return MessageBox.Show(
                message,
                $"❓ {title}",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question,
                MessageBoxResult.Cancel
            );
        }

        // Specific business notifications
        public static void ProductAdded(string productName)
        {
            ShowSuccess($"Product '{productName}' has been added successfully to your inventory.", "Product Added");
        }

        public static void ProductUpdated(string productName)
        {
            ShowSuccess($"Product '{productName}' has been updated successfully.", "Product Updated");
        }

        public static void ProductDeleted(string productName)
        {
            ShowInfo($"Product '{productName}' has been removed from your inventory.", "Product Deleted");
        }

        public static void SaleCompleted(string invoiceNumber, decimal amount)
        {
            ShowSuccess($"Sale completed successfully!\n\nInvoice: {invoiceNumber}\nTotal: Rs. {amount:N2}", "Sale Completed");
        }

        public static void ReturnProcessed(string invoiceNumber, decimal refundAmount)
        {
            ShowSuccess($"Return processed successfully!\n\nInvoice: {invoiceNumber}\nRefund: Rs. {refundAmount:N2}", "Return Processed");
        }

        public static void ValidationError(string fieldName)
        {
            ShowWarning($"Please provide a valid value for '{fieldName}' to continue.", "Validation Required");
        }

        public static void ValidationErrorCustom(string message)
        {
            ShowWarning(message, "Validation Required");
        }

        public static bool ConfirmDelete(string itemName, string itemType = "item")
        {
            return Confirm(
                $"Are you sure you want to delete this {itemType}?\n\n'{itemName}'\n\nThis action cannot be undone.",
                $"Delete {itemType}?"
            );
        }

        public static void OperationFailed(string operation, string reason)
        {
            ShowError($"Failed to {operation}.\n\nReason: {reason}\n\nPlease try again or contact support if the issue persists.", "Operation Failed");
        }
    }
}
