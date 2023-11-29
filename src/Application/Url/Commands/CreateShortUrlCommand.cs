using System.Text;
using FluentValidation;
using HashidsNet;
using MediatR;
using Microsoft.EntityFrameworkCore;
using UrlShortenerService.Application.Common.Interfaces;

namespace UrlShortenerService.Application.Url.Commands;

public record CreateShortUrlCommand : IRequest<string>
{
    public string Url { get; init; } = default!;
}

public class CreateShortUrlCommandValidator : AbstractValidator<CreateShortUrlCommand>
{
    public CreateShortUrlCommandValidator()
    {
        _ = RuleFor(v => v.Url)
          .NotEmpty()
          .WithMessage("Url is required.");

        _ = RuleFor(v => v.Url).Must(uri => Uri.TryCreate(uri, UriKind.Absolute, out _)).When(x => !string.IsNullOrEmpty(x.Url)).WithMessage("Url is not valid.");
    }
}

public class CreateShortUrlCommandHandler : IRequestHandler<CreateShortUrlCommand, string>
{
    private readonly IApplicationDbContext _context;
    private readonly IHashids _hashids;

    public CreateShortUrlCommandHandler(IApplicationDbContext context, IHashids hashids)
    {
        _context = context;
        _hashids = hashids;
    }

    public async Task<string> Handle(CreateShortUrlCommand request, CancellationToken cancellationToken)
    {
        var existingUrlRec = await _context.Urls.FirstOrDefaultAsync(urlRec => urlRec.OriginalUrl == request.Url);
        if (existingUrlRec != null)
        {
            return _hashids.Encode(existingUrlRec.ShortUrl);
        }
        var newUrlRec = new Domain.Entities.Url
        {
            OriginalUrl = request.Url,
            ShortUrl = GetShortUrl(request.Url)
        };
        var result = await _context.Urls.AddAsync(newUrlRec);
        _ = await _context.SaveChangesAsync(cancellationToken);
        return _hashids.Encode(newUrlRec.ShortUrl);

    }

    /// <summary>
    /// Generate short URL from the full URL
    /// </summary>
    /// <param name="fullUrl">Complete URL</param>
    /// <returns>Short code for the URL as an integer</returns>
    private int GetShortUrl(string fullUrl)
    {
        if (fullUrl == null)
            return 0;

        byte[] urlAsBytes = Encoding.UTF8.GetBytes(fullUrl);
        return BitConverter.ToInt32(urlAsBytes, 0);
    }
}
