using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using POSApp.Core.Entities;
using POSApp.Core.Interfaces;
using POSApp.UI.Helpers;

namespace POSApp.UI.ViewModels
{
    public sealed class CategoryManagementViewModel : ViewModelBase
    {
        private readonly ICategoryRepository _categoryRepository;

        private Category? _selectedCategory;
        private string _name = string.Empty;
        private string? _description;

        public ObservableCollection<Category> Categories { get; } = new();

        public Category? SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value) && value != null)
                {
                    LoadCategoryDetails(value);
                }
            }
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string? Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public ICommand AddCommand { get; }
        public ICommand UpdateCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand RefreshCommand { get; }

        public CategoryManagementViewModel(ICategoryRepository categoryRepository)
        {
            _categoryRepository = categoryRepository;

            AddCommand = new RelayCommand(async _ => await AddCategory());
            UpdateCommand = new RelayCommand(async _ => await UpdateCategory(), _ => SelectedCategory != null);
            DeleteCommand = new RelayCommand(async _ => await DeleteCategory(), _ => SelectedCategory != null);
            ClearCommand = new RelayCommand(_ => ClearForm());
            RefreshCommand = new RelayCommand(async _ => await LoadData());

            _ = LoadData();
        }

        private async Task LoadData()
        {
            var categories = await _categoryRepository.GetAllAsync();
            Categories.Clear();
            foreach (var category in categories)
            {
                Categories.Add(category);
            }
        }

        private void LoadCategoryDetails(Category category)
        {
            Name = category.Name;
            Description = category.Description;
        }

        private async Task AddCategory()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                NotificationHelper.ValidationError("Category Name");
                return;
            }

            try
            {
                var category = new Category
                {
                    Name = Name,
                    Description = Description
                };

                await _categoryRepository.AddAsync(category);
                NotificationHelper.ShowSuccess($"Category '{Name}' has been added successfully.", "Category Added");

                await LoadData();
                ClearForm();
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("add category", ex.Message);
            }
        }

        private async Task UpdateCategory()
        {
            if (SelectedCategory == null) return;

            try
            {
                SelectedCategory.Name = Name;
                SelectedCategory.Description = Description;

                await _categoryRepository.UpdateAsync(SelectedCategory);
                NotificationHelper.ShowSuccess($"Category '{Name}' has been updated successfully.", "Category Updated");

                await LoadData();
                ClearForm();
            }
            catch (Exception ex)
            {
                NotificationHelper.OperationFailed("update category", ex.Message);
            }
        }

        private async Task DeleteCategory()
        {
            if (SelectedCategory == null) return;

            if (NotificationHelper.ConfirmDelete(SelectedCategory.Name, "category"))
            {
                try
                {
                    await _categoryRepository.DeleteAsync(SelectedCategory.Id);
                    NotificationHelper.ShowInfo($"Category '{SelectedCategory.Name}' has been removed.", "Category Deleted");

                    await LoadData();
                    ClearForm();
                }
                catch (Exception ex)
                {
                    NotificationHelper.OperationFailed("delete category", ex.Message);
                }
            }
        }

        private void ClearForm()
        {
            SelectedCategory = null;
            Name = string.Empty;
            Description = null;
        }
    }
}
