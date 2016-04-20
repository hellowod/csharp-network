

using System;
using System.Collections.Generic;

namespace net_plugin.net
{
    /// <summary>
    /// Desc: 网络层基类(抽象类)
    /// Author: xiangjinbao
    /// </summary>
    public abstract class NetObject : INetObject
    {
        public const int MAX_BUFFER_SIZE = 65535;
        public const int PACKET_HEAD_SIZE = 8;

        public Queue<byte[]> mSendQueue = new Queue<byte[]>();
        public Queue<byte[]> mRevcQueue = new Queue<byte[]>();

        /// <summary>
        /// 发送消息线程锁
        /// </summary>
        protected readonly object mSendLock = new object();
        /// <summary>
        /// 接受消息线程锁
        /// </summary>
        protected readonly object mRevcLock = new object();

        #region INetObject 成员

        public virtual void Init()
        {
            
        }

        public virtual void Connect(string ip, int port)
        { 
        
        }

        public virtual void Update()
        {
            
        }

        /// <summary>
        /// 发送消息
        /// </summary>
        /// <param name="bytes"></param>
        public virtual void Send(byte[] bytes)
        {
            lock (mSendLock)
            {
                mSendQueue.Enqueue(bytes);
            }
        }

        /// <summary>
        /// 接受收据
        /// </summary>
        /// <returns></returns>
        public virtual byte[] Recv()
        {
            if (mRevcQueue.Count > 0)
            {
                byte[] res;
                lock (mRevcLock)
                {
                    res = mRevcQueue.Dequeue();
                }
                return res;
            }
            else
            {
                return null;
            }
        }

        public virtual void ReConnect()
        {
            
        }

        public virtual void DisConnect()
        {
            
        }

        public virtual bool Connected()
        {
            return false;
        }

        #endregion
    }
}
