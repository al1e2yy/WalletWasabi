using System.Threading.Tasks;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Workflows.ShopinBit;

public partial class ShopinBitWorkflowManagerViewModel : ReactiveObject, IWorkflowManager
{
	private readonly IWorkflowValidator _workflowValidator;

	private readonly string _userName;

	[AutoNotify(SetterModifier = AccessModifier.Private)] private WorkflowViewModel? _currentWorkflow;

	public ShopinBitWorkflowManagerViewModel()
	{
		_workflowValidator = new WorkflowValidatorViewModel();
		_userName = "PussyCat89";
	}

	public IWorkflowValidator WorkflowValidator => _workflowValidator;

	public async Task SendApiRequestAsync()
	{
		// TODO: Just for testing, remove when api service is implemented.
		await Task.Delay(3000);

		if (_currentWorkflow is null)
		{
			return;
		}

		var request = _currentWorkflow.GetResult();

		switch (request)
		{
			case DeliveryWorkflowRequest deliveryWorkflowRequest:
			{
				// TODO:
				break;
			}
			case InitialWorkflowRequest initialWorkflowRequest:
			{
				// TODO:
				break;
			}
			case PackageWorkflowRequest packageWorkflowRequest:
			{
				// TODO:
				break;
			}
			case PaymentWorkflowRequest paymentWorkflowRequest:
			{
				// TODO:
				break;
			}
			case SupportChatWorkflowRequest supportChatWorkflowRequest:
			{
				// TODO:
				break;
			}
			case WorkflowRequestError workflowRequestError:
			{
				// TODO:
				break;
			}
			default:
			{
				throw new ArgumentOutOfRangeException(nameof(request));
			}
		}
	}

	public void SelectNextWorkflow()
	{
		switch (_currentWorkflow)
		{
			case null:
			{
				_currentWorkflow = new InitialWorkflowViewModel(_workflowValidator, _userName);
				break;
			}
			case InitialWorkflowViewModel initialWorkflowViewModel:
			{
				// TODO:
				_currentWorkflow = new DeliveryWorkflowViewModel();
				break;
			}
			case DeliveryWorkflowViewModel deliveryWorkflowViewModel:
			{
				// TODO:
				_currentWorkflow = new PaymentWorkflowViewModel();
				break;
			}
			case PaymentWorkflowViewModel paymentWorkflowViewModel:
			{
				// TODO:
				_currentWorkflow = new PackageWorkflowViewModel();
				break;
			}
			case PackageWorkflowViewModel packageWorkflowViewModel:
			{
				// TODO: After receiving package info switch to final workflow with chat support.
				_currentWorkflow = new SupportChatWorkflowViewModel();
				break;
			}
			case SupportChatWorkflowViewModel supportChatWorkflowViewModel:
			{
				// TODO: Order is complete do nothing?
				break;
			}
		}
	}
}