using FluentValidation;
using MediatR;

namespace BoardMgmt.Application.Common.Behaviors
{
    public sealed class ValidationBehavior<TRequest, TResponse>(
        IEnumerable<IValidator<TRequest>> validators
    ) : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken ct)
        {
            if (validators.Any())
            {
                var ctx = new ValidationContext<TRequest>(request);
                var results = await Task.WhenAll(validators.Select(v => v.ValidateAsync(ctx, ct)));
                var failures = results.SelectMany(r => r.Errors).Where(e => e is not null).ToList();
                if (failures.Count != 0) throw new ValidationException(failures);
            }
            return await next();
        }
    }
}
