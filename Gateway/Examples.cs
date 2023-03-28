using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using NUnit.Framework;
using ShtrihM.Emerald.Integrator.Api.Clients;
using ShtrihM.Emerald.Integrator.Api.Common.Dtos.Documents;
using ShtrihM.Emerald.Integrator.Api.Common;
using ShtrihM.Wattle3.Testing;
using ShtrihM.Wattle3.Json.Extensions;
using System.Security.Cryptography.Pkcs;
using System.Text;
using RestSharp;
using ShtrihM.Emerald.Integrator.Api.Common.Dtos.Documents.DocumentEvents;
using ShtrihM.Emerald.Integrator.Api.Common.Dtos.Tokens;

namespace ShtrihM.Emerald.Examples.Integrator;

/// <summary>
/// Примеры использования API интеграции внешних организаций.
/// </summary>
[TestFixture]
[SuppressMessage("ReSharper", "PrivateFieldCanBeConvertedToLocalVariable")]
public class Examples
{
    /// <summary>
    /// Базовый URL API облачного транспорта.
    /// </summary>
    private static readonly string BaseAddress = $"https://localhost:{Common.Constants.DefaultPortHttpsApiIntegrator}";

    /// <summary>
    /// Приватный сертификат клиента для HTTPS.
    /// </summary>
    private readonly X509Certificate2 m_clientCertificateHttps;

    /// <summary>
    /// Приватный сертификат клиента для создания электронной подписи.
    /// </summary>
    private readonly X509Certificate2 m_clientCertificateSignature;

    /// <summary>
    /// Публичный корневой сертификат сервера для HTTPS.
    /// </summary>
    private readonly X509Certificate2 m_rootServerCertificateHttps;

    /// <summary>
    /// Настроенный клиент HTTPS.
    /// </summary>
    private readonly RestClient m_restClient;

    public Examples()
    {
        var certificateHttpsBytes = File.ReadAllBytes(@"emerald.examples.integrator.https.organization.pfx");
        m_clientCertificateHttps = new X509Certificate2(certificateHttpsBytes, "password");

        var certificateSignatureBytes = File.ReadAllBytes(@"emerald.examples.integrator.signature.organization.pfx");
        m_clientCertificateSignature = new X509Certificate2(certificateSignatureBytes, "password");

        var rootServerCertificateHttpsBytes = File.ReadAllBytes(@"root.emerald.integrator.server.cer");
        m_rootServerCertificateHttps = new X509Certificate2(rootServerCertificateHttpsBytes);

        var handler = Client.NewHttpClientHandler(m_clientCertificateHttps, m_rootServerCertificateHttps, true);
        m_restClient =
            new RestClient(
                useClientFactory: true,
                configureRestClient:
                options =>
                {
                    options.BaseUrl = new Uri(BaseAddress);
                    options.ConfigureMessageHandler = _ => handler;
                },
                configureSerialization:
                config =>
                {
                    Client.UpdateSerializerConfig(config);
                });
    }

    /// <summary>
    /// Создание <see cref="RestClient"/> руками.
    /// </summary>
    [Test]
    public async Task Example_Manual_RestClient()
    {
        var customTrustStore =
            new X509Certificate2Collection
            {
                m_rootServerCertificateHttps
            };
        var customChainPolicy =
            new X509ChainPolicy
            {

                RevocationMode = X509RevocationMode.NoCheck,
                VerificationFlags = X509VerificationFlags.IgnoreWrongUsage,
                TrustMode = X509ChainTrustMode.CustomRootTrust,
            };
        customChainPolicy.CustomTrustStore.AddRange(customTrustStore);

        var handler =
            new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    (_, certificate, chain, _) =>
                    {
                        var customChain =
                            new X509Chain
                            {
                                ChainPolicy = customChainPolicy
                            };
                        customChain.Build(certificate);

                        foreach (var element in customChain.ChainElements)
                        {
                            if (element.Certificate.Thumbprint == m_rootServerCertificateHttps!.Thumbprint)
                            {
                                return true;
                            }
                        }

                        foreach (var element in chain.ChainElements)
                        {
                            if (element.Certificate.Thumbprint == m_rootServerCertificateHttps.Thumbprint)
                            {
                                return true;
                            }
                        }

                        return false;
                    },
            };

        handler.ClientCertificates.Add(m_clientCertificateHttps);

        var restClient =
            new RestClient(
                useClientFactory: true,
                configureRestClient:
                options =>
                {
                    options.BaseUrl = new Uri(BaseAddress);
                    options.ConfigureMessageHandler = _ => handler;
                },
                configureSerialization:
                config =>
                {
                    Client.UpdateSerializerConfig(config);
                });

        using var client = new Client(restClient, true);
        var description = await client.GetDescriptionAsync();

        Assert.IsNotNull(description);
        Console.WriteLine(description.ToJsonText(true));
    }

    /// <summary>
    /// Получить описание сервера.
    /// </summary>
    [Test]
    public async Task Example_GetDescriptionAsync()
    {
        using var client = new Client(m_restClient);
        var description = await client.GetDescriptionAsync();

        Assert.IsNotNull(description);
        Console.WriteLine(description.ToJsonText(true));
    }

    /// <summary>
    /// Отправить документ организаци - создание услуги токена (банковская карта) - билет с ограниченным сроком действия.
    /// </summary>
    [Test]
    public async Task Example_AddDocumentAsync_DocumentTokenBankCardCreateTicketTimeLimited()
    {
        var document =
            new DocumentTokenBankCardCreateTicketTimeLimited
            {
                CreateDate = DateTimeOffset.Now,
                DateBegin = DateTime.Now.Date,
                DateEnd = DateTime.Now.Date.AddDays(30),
                Key = Guid.NewGuid(),
                PanHash = ProviderRandomValues.GetBytes(FieldsConstants.Sha256Length),
                Type = 1,
            };

        using var client = new Client(m_restClient);
        var documentResult = await client.AddDocumentAsync(document, m_clientCertificateSignature);

        Assert.IsNotNull(documentResult);
        Console.WriteLine(documentResult.ToJsonText(true));

        Assert.IsFalse(documentResult.Successful);
        Assert.IsNotNull(documentResult.Id);
        Assert.IsNotNull(documentResult.Events);
        Assert.AreEqual(1, documentResult.Events.Count);

        var @event = documentResult.Events[0] as DocumentEventTokenNotFound;
        Assert.IsNotNull(@event);
        Assert.AreEqual("По PAN банковской карты не найден токен", @event.Reason);
    }

    /// <summary>
    /// Отправить документ организаци - создание услуги токена (банковская карта) - билет с ограниченным сроком действия и ограниченным числом поездок.
    /// </summary>
    [Test]
    public async Task Example_AddDocumentAsync_DocumentTokenBankCardCreateTicketTimeLimitedTravelsLimited()
    {
        var document =
            new DocumentTokenBankCardCreateTicketTimeLimitedTravelsLimited
            {
                CreateDate = DateTimeOffset.Now,
                DateBegin = DateTime.Now.Date,
                DateEnd = DateTime.Now.Date.AddDays(30),
                Key = Guid.NewGuid(),
                PanHash = ProviderRandomValues.GetBytes(FieldsConstants.Sha256Length),
                Type = 1,
                Count = 12,
            };

        using var client = new Client(m_restClient);
        var documentResult = await client.AddDocumentAsync(document, m_clientCertificateSignature);

        Assert.IsNotNull(documentResult);
        Console.WriteLine(documentResult.ToJsonText(true));

        Assert.IsFalse(documentResult.Successful);
        Assert.IsNotNull(documentResult.Id);
        Assert.IsNotNull(documentResult.Events);
        Assert.AreEqual(1, documentResult.Events.Count);

        var @event = documentResult.Events[0] as DocumentEventTokenNotFound;
        Assert.IsNotNull(@event);
        Assert.AreEqual("По PAN банковской карты не найден токен", @event.Reason);
    }

    /// <summary>
    /// Отправить документ организаци.
    /// Электронная подпись создаётся вручную.
    /// </summary>
    [Test]
    public async Task Example_AddDocumentAsync_Mnual_Signature()
    {
        var document =
            new DocumentTokenBankCardCreateTicketTimeLimited
            {
                CreateDate = DateTimeOffset.Now,
                DateBegin = DateTime.Now.Date,
                DateEnd = DateTime.Now.Date.AddDays(30),
                Key = Guid.NewGuid(),
                PanHash = ProviderRandomValues.GetBytes(FieldsConstants.Sha256Length),
                Type = 1,
            };
        var documentText = document.ToJsonText(true);
        var documentBytes = Encoding.UTF8.GetBytes(documentText);
        var contentInfo = new ContentInfo(documentBytes);
        var signedCms = new SignedCms(contentInfo, false);
        var signer =
            new CmsSigner(SubjectIdentifierType.SubjectKeyIdentifier, m_clientCertificateSignature)
            {
                IncludeOption = X509IncludeOption.WholeChain 
            };
        signedCms.ComputeSignature(signer);
        var message = signedCms.Encode();
        var documentMessage =
            new DocumentMessage
            {
                Message = message,
            };

        using var client = new Client(m_restClient);
        var documentResult = await client.AddDocumentAsync(documentMessage);

        Assert.IsNotNull(documentResult);
        Console.WriteLine(documentResult.ToJsonText(true));

        Assert.IsFalse(documentResult.Successful);
        Assert.IsNotNull(documentResult.Id);
        Assert.IsNotNull(documentResult.Events);
        Assert.AreEqual(1, documentResult.Events.Count);

        var @event = documentResult.Events[0] as DocumentEventTokenNotFound;
        Assert.IsNotNull(@event);
        Assert.AreEqual("По PAN банковской карты не найден токен", @event.Reason);
    }

    /// <summary>
    /// Отправить документ организаци.
    /// Электронная подпись создаётся автоматически.
    /// </summary>
    [Test]
    public async Task Example_AddDocumentAsync_Auto_Signature()
    {
        var document =
            new DocumentTokenBankCardCreateTicketTimeLimited
            {
                CreateDate = DateTimeOffset.Now,
                DateBegin = DateTime.Now.Date,
                DateEnd = DateTime.Now.Date.AddDays(30),
                Key = Guid.NewGuid(),
                PanHash = ProviderRandomValues.GetBytes(FieldsConstants.Sha256Length),
                Type = 1,
            };

        using var client = new Client(m_restClient);
        var documentResult = await client.AddDocumentAsync(document, m_clientCertificateSignature);

        Assert.IsNotNull(documentResult);
        Console.WriteLine(documentResult.ToJsonText(true));

        Assert.IsFalse(documentResult.Successful);
        Assert.IsNotNull(documentResult.Id);
        Assert.IsNotNull(documentResult.Events);
        Assert.AreEqual(1, documentResult.Events.Count);

        var @event = documentResult.Events[0] as DocumentEventTokenNotFound;
        Assert.IsNotNull(@event);
        Assert.AreEqual("По PAN банковской карты не найден токен", @event.Reason);
    }

    /// <summary>
    /// Проверить существование PAN банковской карты.
    /// </summary>
    [Test]
    public async Task Example_TokenBankCardExistsAsync()
    {
        using var client = new Client(m_restClient);
        var existsResult =
            await client.TokenBankCardExistsAsync(
                new BankCardPanInfo
                {
                    PanHash = new byte[FieldsConstants.Sha256Length],
                });

        Assert.IsNotNull(existsResult);
        Console.WriteLine(existsResult.ToJsonText(true));

        Assert.IsFalse(existsResult.IsExists);
        Assert.AreEqual("По PAN банковской карты не найден токен", existsResult.Reason);
    }
}
