using NSchema.Operations;

namespace NSchema.Services;

/// <summary>
/// The CLI's full presentation surface.
/// </summary>
internal interface IConsolePresenter : IConsoleMessenger, IOperationReporter;
