using InventorySystem.Core.Entities;
using InventorySystem.Data.Repositories;
using InventorySystem.UI.Commands;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows;

namespace InventorySystem.UI.ViewModels
{
    public class CategoryViewModel : ViewModelBase
    {
        private readonly ICategoryRepository _categoryRepo;

        public ObservableCollection<Category> Categories { get; set; } = new();
        private Category? _selectedCategory;
        public Category? SelectedCategory
        {
            get => _selectedCategory;
            set { _selectedCategory = value; OnPropertyChanged(); }
        }

        public ICommand AddCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand EditCommand { get; } // ✅ New

        public string NewCategoryName { get; set; } = "";

        public CategoryViewModel(ICategoryRepository categoryRepo)
        {
            _categoryRepo = categoryRepo;

            LoadCategories();

            AddCommand = new RelayCommand(async () =>
            {
                if (string.IsNullOrWhiteSpace(NewCategoryName))
                {
                    MessageBox.Show("Category name cannot be empty!");
                    return;
                }

                var category = new Category { Name = NewCategoryName };
                await _categoryRepo.AddAsync(category);
                Categories.Add(category);

                NewCategoryName = "";
                OnPropertyChanged(nameof(NewCategoryName));
            });

            DeleteCommand = new RelayCommand(async () =>
            {
                if (SelectedCategory == null) return;

                await _categoryRepo.DeleteAsync(SelectedCategory);
                Categories.Remove(SelectedCategory);
            });

            // ✅ Edit command
            EditCommand = new RelayCommand(async () =>
            {
                if (SelectedCategory == null) return;

                if (string.IsNullOrWhiteSpace(SelectedCategory.Name))
                {
                    MessageBox.Show("Category name cannot be empty!");
                    return;
                }

                await _categoryRepo.UpdateAsync(SelectedCategory);

                // Refresh list to update UI
                LoadCategories();
            });
        }

        private void LoadCategories()
        {
            Categories.Clear();
            foreach (var c in _categoryRepo.GetAllAsync().Result)
                Categories.Add(c);

            SelectedCategory = Categories.FirstOrDefault();
        }
    }
}