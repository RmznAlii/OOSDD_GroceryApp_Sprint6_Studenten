using CommunityToolkit.Mvvm.Input;
using Grocery.Core.Interfaces.Services;
using Grocery.Core.Models;
using System.Collections.ObjectModel;

namespace Grocery.App.ViewModels
{
    public partial class ProductViewModel : BaseViewModel
    {
        private readonly IProductService _productService;
        private readonly GlobalViewModel _globalViewModel;
        public ObservableCollection<Product> Products { get; set; }

        public ProductViewModel(IProductService productService, GlobalViewModel globalViewModel)
        {
            _productService = productService;
            _globalViewModel = globalViewModel;
            Products = [];
            LoadProducts();
        }

        public override void OnAppearing()
        {
            base.OnAppearing();
            LoadProducts();
        }

        public void LoadProducts()
        {
            Products.Clear();
            foreach (Product p in _productService.GetAll()) 
            {
                Products.Add(p);
            }
        }

        [RelayCommand]
        public async Task NavigateToNewProduct()
        {
            if (_globalViewModel.Client?.Role == Role.Admin) 
            {
                await Shell.Current.GoToAsync(nameof(Views.NewProductView), true);
            }
        }
    }
}