using BiomePampa.Application.DTOs.Customers;
using BiomePampa.Application.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace BiomePampa.ViewModels
{
    public partial class CustomerListViewModel : BaseViewModel
    {
        private readonly ICustomerService _customerService;

        [ObservableProperty]
        private ObservableCollection<CustomerDto> customers = new();

        [ObservableProperty]
        private CustomerDto? selectedCustomer;

        public CustomerListViewModel(ICustomerService customerService)
        {
            _customerService = customerService;
            Title = "Clientes";
        }

        [RelayCommand]
        private async Task LoadCustomersAsync()
        {
            if (IsBusy)
                return;

            try
            {
                IsBusy = true;
                var items = await _customerService.GetAllAsync();
                Customers.Clear();
                foreach (var item in items)
                {
                    Customers.Add(item);
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Erro", $"Não foi possível carregar os clientes: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task AddCustomerAsync()
        {
            await Shell.Current.GoToAsync("customerdetail");
        }

        [RelayCommand]
        private async Task EditCustomerAsync(CustomerDto customer)
        {
            if (customer == null)
                return;

            await Shell.Current.GoToAsync($"customerdetail?id={customer.Id}");
        }

        [RelayCommand]
        private async Task DeleteCustomerAsync(CustomerDto customer)
        {
            if (customer == null)
                return;

            bool confirm = await Shell.Current.DisplayAlert(
                "Confirmar",
                $"Deseja realmente excluir o cliente '{customer.Name}'?",
                "Sim",
                "Não");

            if (!confirm)
                return;

            try
            {
                IsBusy = true;
                await _customerService.DeleteAsync(customer.Id);
                Customers.Remove(customer);
                await Shell.Current.DisplayAlert("Sucesso", "Cliente excluído com sucesso!", "OK");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Erro", $"Não foi possível excluir o cliente: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            await LoadCustomersAsync();
        }
    }
}
