using System;

namespace ET.Server
{
    public abstract class MessageHandler<Request, Response>: IMHandler where Request : MessageObject, IActorRequest where Response : MessageObject, IActorResponse
    {
        protected abstract ETTask Run(Session session, Request request, Response response);

        public void Handle(Session session, object message)
        {
            HandleAsync(session, message).Coroutine();
        }

        private async ETTask HandleAsync(Session session, object message)
        {
            try
            {
                using Request request = message as Request;
                if (request == null)
                {
                    throw new Exception($"消息类型转换错误: {message.GetType().FullName} to {typeof (Request).FullName}");
                }

                int rpcId = request.RpcId;
                long instanceId = session.InstanceId;

                Response response = ObjectPool.Instance.Fetch<Response>();

                try
                {
                    await this.Run(session, request, response);
                }
                catch (Exception exception)
                {
                    Log.Error(exception);
                    response.Error = ErrorCore.ERR_RpcFail;
                    response.Message = exception.ToString();
                }
                
                // 等回调回来,session可以已经断开了,所以需要判断session InstanceId是否一样
                if (session.InstanceId != instanceId)
                {
                    return;
                }
                
                response.RpcId = rpcId; // 在这里设置rpcId是为了防止在Run中不小心修改rpcId字段
                session.Send(response);
            }
            catch (Exception e)
            {
                throw new Exception($"解释消息失败: {message.GetType().FullName}", e);
            }
        }

        public Type GetMessageType()
        {
            return typeof (Request);
        }

        public Type GetResponseType()
        {
            return typeof (Response);
        }
    }
}