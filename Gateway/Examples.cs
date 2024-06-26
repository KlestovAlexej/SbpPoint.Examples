﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using NUnit.Framework;
using ShtrihM.SbpPoint.Gateway.Api.Clients;
using ShtrihM.Wattle3.Json.Extensions;
using RestSharp;
using ShtrihM.SbpPoint.Processing.Api.Common.Dtos.Enterprises.Payments;
using ShtrihM.SbpPoint.Processing.Api.Common.Dtos.Enterprises.Payments.AutomationDynamicQrs;
using ShtrihM.Wattle3.Testing;
using ShtrihM.Wattle3.Utils;
using ShtrihM.SbpPoint.Api.Common;
using ShtrihM.SbpPoint.Processing.Api.Common.Dtos.Enterprises.Payments.Refund;
using ShtrihM.Wattle3.Common.Exceptions;

namespace ShtrihM.SbpPoint.Examples.Gateway;

/// <summary>
/// Примеры использования API шлюза сервера обеспечения взаимодействия с системой быстрых платежей.
/// </summary>
[TestFixture]
[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
[SuppressMessage("ReSharper", "AccessToModifiedClosure")]
public class Examples : BaseExamples
{
    /// <summary>
    /// Базовый URL API шлюза сервера обеспечения взаимодействия с системой быстрых платежей.
    /// </summary>
    private static readonly string BaseAddress = "https://46.28.89.35:9904";

    /// <summary>
    /// Ключ API.
    /// </summary>
    private static readonly string ApiKey = "SrqhLSmIZgaoYU2Df+7SEYWa019UbAd8ai08vzxC8ptNhM8c/zkCY70JKI/hqUKZ";

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
        var rootServerCertificateHttpsBytes = File.ReadAllBytes("root.sbppoint.gateway.server.cer");
        m_rootServerCertificateHttps = new X509Certificate2(rootServerCertificateHttpsBytes);

        var handler = GatewayClient.NewHttpClientHandler(m_rootServerCertificateHttps, true);
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
                config => { GatewayClient.UpdateSerializerConfig(config); });
    }

    /// <summary>
    /// Создание <see cref="RestClient"/> руками.
    /// </summary>
    [Test]
    public async Task Manual_RestClient()
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
                        if (false == customChain.Build(certificate))
                        {
                            return false;
                        }

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
                config => { GatewayClient.UpdateSerializerConfig(config); });

        using var client = new GatewayClient(restClient, true);
        var description = await client.GetDescriptionAsync();

        Assert.IsNotNull(description);
        Console.WriteLine(description.ToJsonText(true));
    }

    /// <summary>
    /// Получить описание сервера.
    /// </summary>
    [Test]
    public async Task GetDescriptionAsync()
    {
        using var client = new GatewayClient(m_restClient);

        var description = await client.GetDescriptionAsync();

        Assert.IsNotNull(description);
        Console.WriteLine(description.ToJsonText(true));
    }

    /// <summary>
    /// Чтение публичной информации ключа API.
    /// </summary>
    [Test]
    public async Task SupportApiKeysReadAsync_Public()
    {
        using var client = new GatewayClient(m_restClient);

        var info = await client.SupportApiKeysReadAsync(ApiKey);
        Assert.IsNotNull(info);
        Console.WriteLine(info.ToJsonText(true));
    }

    /// <summary>
    /// Чтение приватной информации ключа API.
    /// </summary>
    [Test]
    public async Task SupportApiKeysReadAsync_Private()
    {
        using var client = new GatewayClient(m_restClient);

        var info = await client.SupportApiKeysReadAsync(ApiKey, ApiKey);
        Assert.IsNotNull(info);
        Console.WriteLine(info.ToJsonText(true));
    }

    /// <summary>
    /// Программируемый динамический QR-код.
    /// Ручная отмена платежа.
    /// </summary>
    [Test]
    public async Task Payment_AutomationDynamicQr_Cancel_Manual()
    {
        using var client = new GatewayClient(m_restClient);

        // Создание платежа.
        var payment = await client.PaymentsAutomationDynamicQrsCreateAsync(
            ApiKey,
            new AutomationDynamicQrCreate
            {
                Amount = 1000,
                AutoCancelMinutes = 5,
                Purpose = "Тест (10 рублей)"
            });

        // Отмена платежа.
        payment =
            await client.PaymentsAutomationDynamicQrsCancelAsync(
                ApiKey,
                new AutomationDynamicQrCancel
                {
                    Id = payment.Id,
                });
        Assert.IsNotNull(payment);
        Assert.IsTrue(payment.Status.IsFinal);
        Assert.IsFalse(payment.Status.IsSuccess);
        Assert.AreEqual(PaymentStatus.Canceled, payment.Status.Status);

        // Запрос статуса платежа.
        payment = await client.PaymentsAutomationDynamicQrsReadAsync(ApiKey, payment.Id);
        Assert.IsNotNull(payment);
        Console.WriteLine(payment.ToJsonText(true));

        Assert.IsTrue(payment.Status.IsFinal);
        Assert.IsFalse(payment.Status.IsSuccess);
        Assert.AreEqual(PaymentStatus.Canceled, payment.Status.Status);
    }

    /// <summary>
    /// Программируемый динамический QR-код.
    /// Автоматическая отмена платежа.
    /// </summary>
    [Test]
    public async Task Payment_AutomationDynamicQr_Cancel_Auto()
    {
        using var client = new GatewayClient(m_restClient);

        // Создание платежа.
        var payment = await client.PaymentsAutomationDynamicQrsCreateAsync(
            ApiKey,
            new AutomationDynamicQrCreate
            {
                Amount = 1000,
                AutoCancelMinutes = 1,
                Purpose = "Тест (10 рублей)"
            });

        // Ждём завершения платежа - примерно 1 минута.
        WaitHelpers.TimeOut(
            () => client.PaymentsAutomationDynamicQrsReadAsync(ApiKey, payment.Id).SafeGetResult().Status.IsFinal,
            TimeSpan.FromMinutes(5));

        // Запрос статуса платежа.
        payment = await client.PaymentsAutomationDynamicQrsReadAsync(ApiKey, payment.Id);
        Assert.IsNotNull(payment);
        Console.WriteLine(payment.ToJsonText(true));

        Assert.IsTrue(payment.Status.IsFinal);
        Assert.IsFalse(payment.Status.IsSuccess);
        Assert.AreEqual(PaymentStatus.Rejected, payment.Status.Status);
    }

    /// <summary>
    /// Программируемый динамический QR-код.
    /// Оплата платежа.
    /// </summary>
    [Test]
    [Explicit]
    public async Task Payment_AutomationDynamicQr_Image()
    {
        using var client = new GatewayClient(m_restClient);

        // Создание платежа.
        var payment = await client.PaymentsAutomationDynamicQrsCreateAsync(
            ApiKey,
            new AutomationDynamicQrCreate
            {
                Amount = 1000,
                AutoCancelMinutes = 5,
                Purpose = "Тест (10 рублей)"
            });

        ShowQrImage(payment.Link);

        // Ждём завершения платежа - примерно 5 минут.
        WaitHelpers.TimeOut(
            () => client.PaymentsAutomationDynamicQrsReadAsync(ApiKey, payment.Id).SafeGetResult().Status.IsFinal,
            TimeSpan.FromMinutes(10));

        // Запрос статуса платежа.
        payment = await client.PaymentsAutomationDynamicQrsReadAsync(ApiKey, payment.Id);
        Assert.IsNotNull(payment);
        Console.WriteLine(payment.ToJsonText(true));

        Assert.IsTrue(payment.Status.IsFinal);

        DeleteQrImage();
    }

    /// <summary>
    /// Программируемый динамический QR-код.
    /// Оплата платежа.
    /// Возврат платежа.
    /// </summary>
    [Test]
    [Explicit]
    public async Task Payment_Refund()
    {
        using var client = new GatewayClient(m_restClient);

        // Создание платежа.
        var payment = await client.PaymentsAutomationDynamicQrsCreateAsync(
            ApiKey,
            new AutomationDynamicQrCreate
            {
                Amount = 1000,
                AutoCancelMinutes = 5,
                Purpose = "Тест (10 рублей)"
            });

        ShowQrImage(payment.Link);

        // Ждём завершения платежа - примерно 5 минут.
        WaitHelpers.TimeOut(
            () => client.PaymentsAutomationDynamicQrsReadAsync(ApiKey, payment.Id).SafeGetResult().Status.IsFinal,
            TimeSpan.FromMinutes(10));

        // Запрос статуса платежа.
        payment = await client.PaymentsAutomationDynamicQrsReadAsync(ApiKey, payment.Id);
        Assert.IsNotNull(payment);
        Console.WriteLine(payment.ToJsonText(true));
        Assert.IsTrue(payment.Status.IsSuccess);
        Assert.IsFalse(payment.HasRefunds);
        Assert.AreEqual(0, payment.TotalRefundedAmount);

        DeleteQrImage();

        // Возврат платежа на сумму большую суммы платежа.
        var workflowException =
            Assert.ThrowsAsync<WorkflowException>(
                async () => await client.PaymentsRefundsRefundAsync(
                    ApiKey,
                    new RefundCreate
                    {
                        Amount = 5555,
                        Id = payment.Id,
                        Purpose = "Тест",
                    }));
        Assert.AreEqual(WorkflowErrorCodes.RefundAmountOvertopPaymentAmount, workflowException!.Code);

        // Возврат платежа.
        var refund = await client.PaymentsRefundsRefundAsync(
            ApiKey,
            new RefundCreate
            {
                Amount = 1000,
                Id = payment.Id,
                Purpose = "Тест",
            });

        // Ждём завершения возврата.
        WaitHelpers.TimeOut(
            () => client.PaymentsRefundsReadAsync(ApiKey, refund.Id).SafeGetResult().IsFinal,
            TimeSpan.FromMinutes(10));

        // Запрос статуса возврата.
        refund = await client.PaymentsRefundsReadAsync(ApiKey, refund.Id);
        Assert.IsNotNull(refund);
        Console.WriteLine(refund.ToJsonText(true));
        Assert.IsTrue(refund.IsSuccess);

        // Запрос статуса платежа.
        payment = await client.PaymentsAutomationDynamicQrsReadAsync(ApiKey, payment.Id);
        Assert.IsNotNull(payment);
        Console.WriteLine(payment.ToJsonText(true));
        Assert.IsTrue(payment.Status.IsSuccess);
        Assert.IsTrue(payment.HasRefunds);
        Assert.AreEqual(1000, payment.TotalRefundedAmount);
    }

    /// <summary>
    /// Программируемый динамический QR-код.
    /// Оплата платежа.
    /// Возврат платежа частями.
    /// </summary>
    [Test]
    [Explicit]
    public async Task Payment_Refund_Partial()
    {
        using var client = new GatewayClient(m_restClient);

        // Создание платежа.
        var payment = await client.PaymentsAutomationDynamicQrsCreateAsync(
            ApiKey,
            new AutomationDynamicQrCreate
            {
                Amount = 1000,
                AutoCancelMinutes = 5,
                Purpose = "Тест (10 рублей)"
            });

        ShowQrImage(payment.Link);

        // Ждём завершения платежа - примерно 5 минут.
        WaitHelpers.TimeOut(
            () => client.PaymentsAutomationDynamicQrsReadAsync(ApiKey, payment.Id).SafeGetResult().Status.IsFinal,
            TimeSpan.FromMinutes(10));

        // Запрос статуса платежа.
        payment = await client.PaymentsAutomationDynamicQrsReadAsync(ApiKey, payment.Id);
        Assert.IsNotNull(payment);
        Console.WriteLine(payment.ToJsonText(true));
        Assert.IsTrue(payment.Status.IsSuccess);
        Assert.IsFalse(payment.HasRefunds);
        Assert.AreEqual(0, payment.TotalRefundedAmount);

        DeleteQrImage();

        {
            // Возврат платежа.
            var refund = await client.PaymentsRefundsRefundAsync(
                ApiKey,
                new RefundCreate
                {
                    Amount = 500,
                    Id = payment.Id,
                    Purpose = "Тест",
                });

            // Ждём завершения возврата.
            WaitHelpers.TimeOut(
                () => client.PaymentsRefundsReadAsync(ApiKey, refund.Id).SafeGetResult().IsFinal,
                TimeSpan.FromMinutes(10));

            // Запрос статуса возврата.
            refund = await client.PaymentsRefundsReadAsync(ApiKey, refund.Id);
            Assert.IsNotNull(refund);
            Console.WriteLine(refund.ToJsonText(true));
            Assert.IsTrue(refund.IsSuccess);
        }

        {
            // Возврат платежа.
            var refund = await client.PaymentsRefundsRefundAsync(
                ApiKey,
                new RefundCreate
                {
                    Amount = 500,
                    Id = payment.Id,
                    Purpose = "Тест",
                });

            // Ждём завершения возврата.
            WaitHelpers.TimeOut(
                () => client.PaymentsRefundsReadAsync(ApiKey, refund.Id).SafeGetResult().IsFinal,
                TimeSpan.FromMinutes(10));

            // Запрос статуса возврата.
            refund = await client.PaymentsRefundsReadAsync(ApiKey, refund.Id);
            Assert.IsNotNull(refund);
            Console.WriteLine(refund.ToJsonText(true));
            Assert.IsTrue(refund.IsSuccess);
        }

        // Возврат платежа уже полность возвращенного.
        var workflowException =
            Assert.ThrowsAsync<WorkflowException>(
                async () => await client.PaymentsRefundsRefundAsync(
                    ApiKey,
                    new RefundCreate
                    {
                        Amount = 1000,
                        Id = payment.Id,
                        Purpose = "Тест",
                    }));
        Assert.AreEqual(WorkflowErrorCodes.RefundAmountOvertopPaymentAllowedRefundAmount, workflowException!.Code);

        // Запрос статуса платежа.
        payment = await client.PaymentsAutomationDynamicQrsReadAsync(ApiKey, payment.Id);
        Assert.IsNotNull(payment);
        Console.WriteLine(payment.ToJsonText(true));
        Assert.IsTrue(payment.Status.IsSuccess);
        Assert.IsTrue(payment.HasRefunds);
        Assert.AreEqual(1000, payment.TotalRefundedAmount);
    }
}
