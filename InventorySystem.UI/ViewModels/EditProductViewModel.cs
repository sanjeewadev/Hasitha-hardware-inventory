using InventorySystem.Core.Entities;
using InventorySystem.Data.Repositories;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace InventorySystem.UI.ViewModels
{
    public class EditProductViewModel : AddProductViewModel
    {
        public EditProductViewModel(Product product,
                                    IProductRepository productRepo,
                                    ICategoryRepository categoryRepo,
                                    System.Action closeWindowAction)
            : base(productRepo, categoryRepo, closeWindowAction)
        {
            // Pre-fill fields
            Name = product.Name;
            BuyingPrice = product.BuyingPrice;
            SellingPrice = product.SellingPrice;
            Quantity = product.Quantity;
            SelectedCategory = product.Category;

            SaveCommand = new Commands.RelayCommand(() =>
            {
                product.Name = Name;
                product.BuyingPrice = BuyingPrice;
                product.SellingPrice = SellingPrice;
                product.Quantity = Quantity;
                product.Category = SelectedCategory!;

                productRepo.UpdateAsync(product).Wait();
                closeWindowAction();
            });
        }
    }
}