﻿using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace WebApiClientCore.Extensions.OAuths.HttpMessageHandlers
{
    /// <summary>
    /// 表示OAuth授权token应用的http消息处理程序抽象
    /// </summary>
    public abstract class OAuthTokenHandler : DelegatingHandler
    {
        /// <summary>
        /// token提供者
        /// </summary>
        private readonly ITokenProvider tokenProvider;

        /// <summary>
        /// OAuth授权token应用的http消息处理程序抽象
        /// </summary>
        /// <param name="serviceProvider">服务提供者</param>
        /// <param name="httpApiType">接口类型</param>
        public OAuthTokenHandler(IServiceProvider serviceProvider, Type httpApiType)
        {
            this.tokenProvider = this.GetTokenProvider(serviceProvider, httpApiType);
        }

        /// <summary>
        /// 获取token提供者
        /// </summary>
        /// <param name="serviceProvider">服务提供者</param>
        /// <param name="httpApiType">接口类型</param>
        /// <returns></returns>
        protected abstract ITokenProvider GetTokenProvider(IServiceProvider serviceProvider, Type httpApiType);

        /// <summary>
        /// 检测响应是否未授权
        /// 未授权则重试请求一次
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected sealed override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await this.SendCoreAsync(request, cancellationToken).ConfigureAwait(false);
            if (await this.IsUnauthorizedAsync(response).ConfigureAwait(false) == true)
            {
                this.tokenProvider.ClearToken();
                response = await this.SendCoreAsync(request, cancellationToken).ConfigureAwait(false);
            }
            return response;
        }

        /// <summary>
        /// 发送http请求
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var tokenResult = await this.tokenProvider.GetTokenAsync().ConfigureAwait(false);
            this.UseTokenResult(request, tokenResult);
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 返回响应是否为未授权状态
        /// 反回true则强制清除token以支持下次获取到新的token
        /// </summary>
        /// <param name="response"></param> 
        protected virtual Task<bool> IsUnauthorizedAsync(HttpResponseMessage? response)
        {
            var state = response != null && response.StatusCode == HttpStatusCode.Unauthorized;
            return Task.FromResult(state);
        }

        /// <summary>
        /// 应用token
        /// 默认为添加到请求头的Authorization
        /// </summary>
        /// <param name="request">请求上下文</param>
        /// <param name="tokenResult">token结果</param>
        /// <returns></returns>
        protected virtual void UseTokenResult(HttpRequestMessage request, TokenResult tokenResult)
        {
            var tokenType = tokenResult.Token_type ?? "Bearer";
            request.Headers.Authorization = new AuthenticationHeaderValue(tokenType, tokenResult.Access_token);
        }
    }
}