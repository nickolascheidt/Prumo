using BiomePampa.Application.DTOs.Customers;
using BiomePampa.Application.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BiomePampa.ViewModels
{
    [QueryProperty(nameof(CustomerIdParam), "id")]
    public partial class CustomerDetailViewModel : BaseViewModel
    {
        private readonly ICustomerService _customerService;

        [ObservableProperty]
        private string customerIdParam = string.Empty;

        [ObservableProperty]
        private string customerName = string.Empty;

        [ObservableProperty]
        private string customerCompanyName = string.Empty;

        [ObservableProperty]
        private string customerTaxId = string.Empty;

        [ObservableProperty]
        private string customerPhone = string.Empty;

        [ObservableProperty]
        private string customerEmail = string.Empty;

        [ObservableProperty]
        private string customerAddress = string.Empty;

        [ObservableProperty]
        private string customerCity = string.Empty;

        [ObservableProperty]
        private string customerState = string.Empty;

        [ObservableProperty]
        private string customerZipCode = string.Empty;

        [ObservableProperty]
        private string customerNotes = string.Empty;

        public CustomerDetailViewModel(ICustomerService customerService)
        {
            _customerService = customerService;
        }

        [RelayCommand]
        private async Task LoadCustomerAsync()
        {
            if (string.IsNullOrEmpty(CustomerIdParam) || !Guid.TryParse(CustomerIdParam, out var customerId))
            {
                Title = "Novo Cliente";
                return;
            }

            try
            {
                IsBusy = true;
                Title = "Editar Cliente";

                var customer = await _customerService.GetByIdAsync(customerId);
                if (customer != null)
                {
                    CustomerName = customer.Name;
                    CustomerCompanyName = customer.CompanyName ?? string.Empty;
                    CustomerTaxId = customer.TaxId;
                    CustomerPhone = customer.Phone ?? string.Empty;
                    CustomerEmail = customer.Email ?? string.Empty;
                    CustomerAddress = customer.Address ?? string.Empty;
                    CustomerCity = customer.City ?? string.Empty;
                    CustomerState = customer.State ?? string.Empty;
                    CustomerZipCode = customer.ZipCode ?? string.Empty;
                    CustomerNotes = customer.Notes ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Erro", $"Não foi possível carregar o cliente: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (IsBusy)
                return;

            if (string.IsNullOrWhiteSpace(CustomerName))
            {
                await Shell.Current.DisplayAlert("Validação", "Nome é obrigatório", "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(CustomerTaxId))
            {
                await Shell.Current.DisplayAlert("Validação", "CPF/CNPJ é obrigatório", "OK");
                return;
            }

            try
            {
                IsBusy = true;

                if (string.IsNullOrEmpty(CustomerIdParam) || !Guid.TryParse(CustomerIdParam, out var customerId))
                {
                    var createDto = new CreateCustomerDto(
                        CustomerName,
                        string.IsNullOrWhiteSpace(CustomerCompanyName) ? null : CustomerCompanyName,
                        CustomerTaxId,
                        string.IsNullOrWhiteSpace(CustomerPhone) ? null : CustomerPhone,
                        string.IsNullOrWhiteSpace(CustomerEmail) ? null : CustomerEmail,
                        string.IsNullOrWhiteSpace(CustomerAddress) ? null : CustomerAddress,
                        string.IsNullOrWhiteSpace(CustomerCity) ? null : CustomerCity,
                        string.IsNullOrWhiteSpace(CustomerState) ? null : CustomerState,
                        string.IsNullOrWhiteSpace(CustomerZipCode) ? null : CustomerZipCode,
                        string.IsNullOrWhiteSpace(CustomerNotes) ? null : CustomerNotes
                    );

                    await _customerService.CreateAsync(createDto);
                    await Shell.Current.DisplayAlert("Sucesso", "Cliente criado com sucesso!", "OK");
                }
                else
                {
                    var updateDto = new UpdateCustomerDto(
                        CustomerName,
                        string.IsNullOrWhiteSpace(CustomerCompanyName) ? null : CustomerCompanyName,
                        CustomerTaxId,
                        string.IsNullOrWhiteSpace(CustomerPhone) ? null : CustomerPhone,
                        string.IsNullOrWhiteSpace(CustomerEmail) ? null : CustomerEmail,
                        string.IsNullOrWhiteSpace(CustomerAddress) ? null : CustomerAddress,
                        string.IsNullOrWhiteSpace(CustomerCity) ? null : CustomerCity,
                        string.IsNullOrWhiteSpace(CustomerState) ? null : CustomerState,
                        string.IsNullOrWhiteSpace(CustomerZipCode) ? null : CustomerZipCode,
                        string.IsNullOrWhiteSpace(CustomerNotes) ? null : CustomerNotes
                    );

                    await _customerService.UpdateAsync(customerId, updateDto);
                    await Shell.Current.DisplayAlert("Sucesso", "Cliente atualizado com sucesso!", "OK");
                }

                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Erro", $"Não foi possível salvar o cliente: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task CancelAsync()
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
