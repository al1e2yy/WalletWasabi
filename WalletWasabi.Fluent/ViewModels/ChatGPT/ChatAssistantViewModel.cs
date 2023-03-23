using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AI.Model.Services;
using AI.Services;
using ChatGPT.Model.Services;
using ChatGPT.Services;
using ChatGPT.ViewModels.Chat;
using ChatGPT.ViewModels.Settings;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.ChatGPT;

public partial class ChatAssistantViewModel : ReactiveObject
{
	static ChatAssistantViewModel()
	{
		ConfigureServices();
	}

	private string _initialDirections = """
You are a helpful assistant named Wasabito, you are Wasabi Wallet operator.
I will write text prompts and you will generate appropriate answers
only in json format I have provided, do not add text before or after json message.

The json format for the answers is as follows:
{
  "status": "",
  "message": "",
}
The status and message properties are of type string.

The json response "status" property value can be one of the following:
- "command":  when wasabi C# scripting api command is the answer
- "error": when not possible to answer or other problem
- "message": when answer is only text message but not error

When "status"="command" the "message" value can only be set to
one of the following wasabi api C# scripting commands which will be executed as C# script:
- public async Task<string> Send(string address, string amount)
  command requires address and amount parameters
- public async Task<string> Receive(string[] labels)
  command requires labels array parameter
- public async Task<string> Balance()
  command does not require any parameters
e.g. for Send command (other follow similar pattern):
{
  "status": "command",
  "message": "await Send("valid BTC address", "valid BTC amount")",
}

If user does not provide valid param to execute api command please set status=error and ask followup question to provide that info:
e.g.:
{
  "status": "error",
  "message": "Please provide valid address for send.",
}

If not enough info is provided to execute command please set status=error and ask user to provide missing information:
e.g.:
{
  "status": "error",
  "message": "Please provide valid address.",
}

If user ask question the answer please set status=message and ask user is in following format:
{
  "status": "message",
  "message": "The address used the following format...",
}

You will always write answers only as json response.
Do not add additional text before or after json.
""";

	private ChatViewModel _chat;
	private CancellationTokenSource _cts;

	[AutoNotify] private bool _isChatListVisible;
	[AutoNotify] private string? _inputText = "";

	public ChatAssistantViewModel()
	{
		_chat = new ChatViewModel
		{
			Settings = new ChatSettingsViewModel
			{
				Temperature = 0.7m,
				TopP = 1m,
				MaxTokens = 2000,
				Model = "gpt-3.5-turbo"
			}
		};

		_chat.AddSystemMessage(_initialDirections);

		SendCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			var inputText = InputText;

			if (!string.IsNullOrWhiteSpace(inputText))
			{
				InputText = "";

				await SendAsync(inputText);
			}
		});

		Messages = new ObservableCollection<MessageViewModel>();
	}

	public ICommand? SendCommand { get; protected set; }

	public ObservableCollection<MessageViewModel> Messages { get; }

	private async Task SendAsync(string input)
	{
		try
		{
			Messages.Add(new UserMessageViewModel()
			{
				Message = input
			});

			_chat.AddUserMessage(input);

			_cts = new CancellationTokenSource();
			var result = await _chat.SendAsync(_chat.CreateChatMessages(), _cts.Token);

			if (result?.Message is { } assistantResultString)
			{
				_chat.AddAssistantMessage(assistantResultString);

				Console.WriteLine(assistantResultString);

				AssistantResult? assistantResult;
				string resultMessage = "";

				try
				{
					assistantResult = JsonConvert.DeserializeObject<AssistantResult>(assistantResultString);
					if (assistantResult is { })
					{
						var message = assistantResult.Message;

						if (assistantResult.Status == "command")
						{
							if (message is { })
							{
								var globals = new ChatAssistantScriptGlobals
								{
									Chat = this,
									Main = MainViewModel.Instance
								};
								resultMessage = await CSharpScript.EvaluateAsync<string>(message, globals: globals);
							}
						}
						else if (assistantResult.Status == "error")
						{
							if (message is { })
							{
								resultMessage = message;
							}
							else
							{
								// TODO:
								resultMessage = message;
							}
						}
						else if (assistantResult.Status == "message")
						{
							if (message is { })
							{
								resultMessage = message;
							}
							else
							{
								// TODO:
								resultMessage = message;
							}
						}
					}
				}
				catch (Exception e)
				{
					Console.WriteLine(e);
					resultMessage = assistantResultString;
				}

				Messages.Add(new AssistantMessageViewModel
				{
					Message = resultMessage
				});
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine("Error: " + ex.Message);
		}
	}

	private static void ConfigureServices()
	{
		IServiceCollection serviceCollection = new ServiceCollection();

		serviceCollection.AddSingleton<IStorageFactory, IsolatedStorageFactory>();
		serviceCollection.AddSingleton<IChatService, ChatService>();

		serviceCollection.AddTransient<ChatMessageViewModel>();
		serviceCollection.AddTransient<ChatSettingsViewModel>();
		serviceCollection.AddTransient<ChatResultViewModel>();
		serviceCollection.AddTransient<ChatViewModel>();
		serviceCollection.AddTransient<PromptViewModel>();

		Ioc.Default.ConfigureServices(serviceCollection.BuildServiceProvider());
	}
}
