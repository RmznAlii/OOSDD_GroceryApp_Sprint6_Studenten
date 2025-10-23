using CommunityToolkit.Mvvm.ComponentModel;
using Grocery.Core.Interfaces.Services;
using Grocery.Core.Models;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;

namespace Grocery.App.ViewModels
{
    /// <summary>
    /// ViewModel voor het aanmaken van nieuwe producten in de app.
    /// Zorgt voor invoervalidatie, foutmeldingen en het opslaan van producten via de service-laag.
    /// </summary>
    public partial class NewProductViewModel : BaseViewModel
    {
        private readonly IProductService _productService;

        /// <summary>
        /// Lijst met alle bestaande producten (wordt vernieuwd na toevoegen).
        /// </summary>
        public ObservableCollection<Product> Products { get; set; }

        // Gebruikersinvoer
        [ObservableProperty] private string name = "";
        [ObservableProperty] private string stock = "";
        [ObservableProperty] private string shelfLife = "";
        [ObservableProperty] private string price = "";

        // Algemene foutmelding
        [ObservableProperty] private string errorMessage = "";

        // Veldspecifieke foutmeldingen
        [ObservableProperty] private string nameError = "";
        [ObservableProperty] private string stockError = "";
        [ObservableProperty] private string dateError = "";
        [ObservableProperty] private string priceError = "";

        /// <summary>
        /// Constructor: initialiseert de service en laadt alle bestaande producten.
        /// </summary>
        public NewProductViewModel(IProductService productService)
        {
            _productService = productService;
            Products = new();
            foreach (Product p in _productService.GetAll())
                Products.Add(p);
        }

        /// <summary>
        /// Command om een nieuw product toe te voegen.
        /// Voert validatie uit en toont foutmeldingen indien nodig.
        /// Bij succes wordt het product opgeslagen en de lijst vernieuwd.
        /// </summary>
        [RelayCommand]
        private async Task AddProduct()
        {
            // Reset foutmeldingen
            ErrorMessage = "";
            NameError = "";
            StockError = "";
            DateError = "";
            PriceError = "";

            bool hasErrors = false;

            // Controleer of alle velden zijn ingevuld
            if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Stock) ||
                string.IsNullOrWhiteSpace(ShelfLife) || string.IsNullOrWhiteSpace(Price))
            {
                ErrorMessage = "Vul alle velden in.";
                return;
            }

            // Controleer de naam
            if (Name.Length < 2 || Name.Length > 50)
            {
                NameError = "Naam moet tussen 2 en 50 tekens zijn.";
                hasErrors = true;
            }

            // Controleer de voorraad (moet positief geheel getal zijn)
            if (!int.TryParse(Stock, out int stockValue) || stockValue < 0)
            {
                StockError = "Voorraad moet een positief geheel getal zijn.";
                hasErrors = true;
            }

            // Controleer de datum (moet in formaat dd-MM-jjjj zijn en in de toekomst liggen)
            if (!DateOnly.TryParseExact(ShelfLife, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly shelfLifeValue))
            {
                DateError = "Voer datum in als dd-mm-jjjj.";
                hasErrors = true;
            }
            else if (shelfLifeValue < DateOnly.FromDateTime(DateTime.Now))
            {
                DateError = "Datum moet in de toekomst liggen.";
                hasErrors = true;
            }

            // Controleer de prijs (moet tussen 0 en 999.99 liggen, . of , toegestaan)
            if (!decimal.TryParse(Price.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal priceValue) 
                || priceValue < 0 || priceValue > 999.99m)
            {
                PriceError = "Prijs moet tussen 0 en 999.99 zijn.";
                hasErrors = true;
            }

            // Als er geen fouten zijn, sla het product op
            if (!hasErrors)
            {
                try
                {
                    var newProduct = new Product(0, Name.Trim(), stockValue, shelfLifeValue, priceValue);
                    var addedProduct = _productService.Add(newProduct);
                    Products.Add(addedProduct);

                    // Velden leegmaken na succesvol opslaan
                    Name = "";
                    Stock = "";
                    ShelfLife = "";
                    Price = "";
                    
                    // Navigeer terug naar de ProductView zodat de lijst ververst wordt
                    await Shell.Current.GoToAsync("..", true);
                }
                catch (Exception ex)
                {
                    ErrorMessage = $"Er is een fout opgetreden bij het toevoegen: {ex.Message}";
                }
            }
        }
    }
}
