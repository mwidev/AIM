/*
 * Copyright (C)2014 Araz Farhang Dareshuri
 * This file is a part of Aegis Implict Ssl Mailer (AIM)
 * Aegis Implict Ssl Mailer is free software: 
 * you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
 * This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  
 * See the GNU General Public License for more details.
 * You should have received a copy of the GNU General Public License along with this program.  
 * If not, see <http://www.gnu.org/licenses/>.
 * If you need any more details please contact <a.farhang.d@gmail.com>
 * Aegis Implict Ssl Mailer is an implict ssl package to use mine/smime messages on implict ssl servers
 */
using System;
using System.ComponentModel;
using System.Net.Mail;
using System.Runtime.Remoting;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using AegisImplictMail;

namespace AegisImplicitMail
{

    /// <summary>
    /// Send Implicit Ssl and none Ssl Messages
    /// </summary>
    public class SmtpSocketClient : IDisposable
    {
        const string AuthExtension = "AUTH";
        const string AuthNtlm = "NTLM";

        private const string Gap = " ";
        const string AuthGssapi = "gssapi";
        const string AuthWDigest = "wdigest";

        /// <summary>
        /// Sets the transaction time out by defualt it is 100,000  (100 secconds)
        /// </summary>
        public int Timeout {
            get { return _timeout; }
            set { _timeout = value; } }
        #region variables
		/// <summary>
		/// Delegate for mail sent notification.
		/// </summary>


		/// <summary>
		/// The delegate function which is called after mail has been sent.
		/// </summary>
		public event SendCompletedEventHandler SendCompleted;
        private SmtpSocketConnection _con;
        private int _port;
        private int _timeout = 100000;
        private readonly bool _sendAsHtml;
        private AuthenticationType _authMode = AuthenticationType.UseDefualtCridentials;
        private string _user;
        private string _password;
        private MimeMailMessage _mailMessage;
        private X509CertificateCollection ClientCertificates { get; set; }

        private string _host;
        /// <summary>
        /// Name of server.
        /// </summary>
        public string Host
        {
            get { return _host; }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException("Host shouldn't be empty or null. Invalid host name.");
                }
                _host = value;
            }
        }

        /// <summary>
		/// Port number of server server.
		/// </summary>
		public int Port
		{
			get {return _port;}
			set 
			{
				if(value <= 0)
				{
					throw new ArgumentException("Invalid port.");
				}
				_port = value;
			}
		}

      
    /// <summary>
    /// Method used for authentication.
    /// </summary>
    public AuthenticationType AuthenticationMode
    {
      get {return _authMode;}
      set {_authMode = value;}
    }

    /// <summary>
    /// User ID for authentication.
    /// </summary>
    public string User
    {
      get {return _user;}
      set {_user = value;}
    }

    /// <summary>
    /// Password for authentication.
    /// </summary>
    public string Password
    {
      get {return _password;}
      set {_password = value;}
    }

        public MimeMailMessage MailMessage
        {
            get { return _mailMessage; }
            set { _mailMessage = value; }
        }

        public bool EnableSsl {get; set; }

        public bool DsnEnabled {get; private set; }

        public bool ServerSupportsEai {get; private set; }

        public bool SupportsTls {get; private set; }
   

        #endregion

        #region cunstructor

        /// <summary>
        /// Generate a smtp socket client object
        /// </summary>
        /// <param name="host">Host address</param>
        /// <param name="port">Port Number</param>
        /// <param name="username">User name to login into server</param>
        /// <param name="password">Password</param>
        /// <param name="authenticationMode">Mode of authentication</param>
        /// <param name="useHtml">Determine if mail message is html or not</param>
        /// <param name="msg">Message to send</param>
        /// <param name="onMailSend">This function will be called after mail is sent</param>
        /// <param name="enableSsl">Your connection is Ssl conection?</param>
        /// <exception cref="ArgumentNullException">If username and pass is needed and not provided</exception>
        public SmtpSocketClient(string host, int port =465, string username =null, string password = null, AuthenticationType authenticationMode = AuthenticationType.Base64, bool useHtml =true, MimeMailMessage msg =null , SendCompletedEventHandler onMailSend =null, bool enableSsl = true ):this(msg)
        {
            if ((AuthenticationMode != AuthenticationType.UseDefualtCridentials) &&
                (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)))
            {
                throw new ArgumentNullException("username");
            }
            
            _host = host;
            _port = port;
            _user = username;
            _password = password;
            _authMode = authenticationMode;
            _mailMessage = msg;
            _sendAsHtml = useHtml;
            SendCompleted = onMailSend;
            EnableSsl = enableSsl;

        }

        public SmtpSocketClient(MimeMailMessage msg =null, bool enableSsl =true)
        {
		    if (msg == null)
		    {
		        msg = new MimeMailMessage();
		    }
            _mailMessage = msg;
            EnableSsl = enableSsl;
        }

#endregion


        public bool TestConnection()
        {
            lock (this)
            {
                if (string.IsNullOrWhiteSpace(_host))
                {
                    throw new ArgumentException("There wasn't any host address found for the mail.");
                }
                if (_authMode != AuthenticationType.UseDefualtCridentials)
                {
                    if (string.IsNullOrWhiteSpace(_user))
                    {
                        throw new ArgumentException(
                            "You must specify user name when you are not using defualt credentials");
                    }

                    if (string.IsNullOrWhiteSpace(_password))
                    {
                        throw new ArgumentException(
                            "You must specify password when you are not using defualt credentials");
                    }
                }

                if (InCall)
                {
                    throw new InvalidOperationException("Mime mailer is busy already, please try later");
                }

                InCall = true;
                //set up initial connection
                return EsablishSmtp();
            }
        }

        private bool EsablishSmtp()
        {
            _con = new SmtpSocketConnection();
            if (ClientCertificates != null)
            {
                _con.clientcerts = ClientCertificates;
            }
            if (_port <= 0) _port = 465;
            try
            {
                _con.Open(_host, _port, EnableSsl,Timeout);
            }
            catch (Exception err)
            {
                if (SendCompleted != null)
                {
                    SendCompleted(this,
                        new AsyncCompletedEventArgs(
                            err, true, err.Message));
                }
                Dispose();
                return false;
            }
            string response;
            int code;
            //read greeting
            _con.GetReply(out response, out code);
       
            //Code 220 means that service is up and working

            if (code != 220)
            {
                //There is something wrong
                if (code == 421)
                {
                    if (SendCompleted != null)
                    {
                        SendCompleted(this,
                            new AsyncCompletedEventArgs(
                                new ServerException("Service not available, closing transmission channel"), true,
                                response));
                    }
                }
                else
                {
                    if (SendCompleted != null)
                    {
                        SendCompleted(this,
                            new AsyncCompletedEventArgs(
                                new ServerException("We couldn't connect to server, server is clossing"), true,
                                response));
                    }
                }
                QuiteConnection(out response, out code);
                return false;
            }
            var buf = new StringBuilder();
            if (_authMode == AuthenticationType.UseDefualtCridentials)
            {
                buf.Append(SmtpCommands.Hello);
                buf.Append(_host);
                _con.SendCommand(buf.ToString());
                _con.GetReply(out response, out code);
                //todo : Check authentication Errors
            }
            else
            {
                buf.Append(SmtpCommands.EHello);
                buf.Append(_host);
                _con.SendCommand(buf.ToString());
                // Get available command in the EHello Answer
                _con.GetReply(out response, out code);

                string[] lines = response.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);
                ParseExtensions(lines);

                Console.Out.WriteLine("Reply to EHLO: " + response + " Code :" + code);
                switch (_authMode)
                {
                    case AuthenticationType.Base64:
                   
                        if (!AuthenticateAsBase64(out response, out code))
                        {
                            if (code == 501)
                            {
                                if (SendCompleted != null)
                                {
                                    SendCompleted(this,
                                        new AsyncCompletedEventArgs(
                                         new ServerException("Service Does not support Base64 Encoding. Please check authentification type"), true, response));
                                }
                            }
                            if (code == 535)
                            {
                                if (SendCompleted != null)
                                {
                                    SendCompleted(this,
                                        new AsyncCompletedEventArgs(
                                            new ServerException("SMTP client authenticates but the username or password is incorrect"), true, response));
                                }
                            }
                            else
                            {
                                if (SendCompleted != null)
                                {
                                    SendCompleted(this,
                                        new AsyncCompletedEventArgs(
                                            new ServerException("Authenticiation Failed"), true, response));
                                }
                            }
                            QuiteConnection(out response, out code);
                            return false;
                        }

                        break;

                    case AuthenticationType.PlainText:

                        if (!AuthenticateAsPlainText(out response, out code))
                        {
                            if (code == 501)
                            {
                                if (SendCompleted != null)
                                {
                                    SendCompleted(this,
                                        new AsyncCompletedEventArgs(
                                         new ServerException("Service Does not support Base64 Encoding. Please check authentification type"), true, response));
                                }
                            }
                            if (code == 535)
                            {
                                if (SendCompleted != null)
                                {
                                    SendCompleted(this,
                                        new AsyncCompletedEventArgs(
                                            new ServerException("SMTP client authenticates but the username or password is incorrect"), true, response));
                                }
                            }
                            else
                            {
                                if (SendCompleted != null)
                                {
                                    SendCompleted(this,
                                        new AsyncCompletedEventArgs(
                                            new ServerException("Authenticiation Failed"), true, response));
                                }
                            }
                            QuiteConnection(out response, out code);
                            return false;
                        }
                        break;
                }
            }
    
            return true;
        }

        #region MessageSenders

        /// <summary>
        /// Send the message.
        /// </summary>
        public void SendMail(AbstractMailMessage message)
        {
            MailMessage = (MimeMailMessage) message;
            lock (this)
            {
                if (string.IsNullOrWhiteSpace(_host))
                {
                    throw new ArgumentException("There wasn't any host address found for the mail.");
                }
                if (_authMode != AuthenticationType.UseDefualtCridentials)
                {
                    if (string.IsNullOrWhiteSpace(_user))
                    {
                        throw new ArgumentException(
                            "You must specify user name when you are not using defualt credentials");
                    }

                    if (string.IsNullOrWhiteSpace(_password))
                    {
                        throw new ArgumentException(
                            "You must specify password when you are not using defualt credentials");
                    }
                }

                if (InCall)
                {
                    throw new InvalidOperationException("Mime mailer is busy already, please try later");
                }

                if (String.IsNullOrEmpty(MailMessage.From.Address))
                {
                    throw new Exception("There wasn't any sender for the message");
                }
                if (MailMessage.To.Count == 0)
                {
                    throw new Exception("Please specifie at least one reciever for the message");
                }

                InCall = true;
                //set up initial connection
                if (EsablishSmtp())
                {
                    string response;
                    int code;
                    var buf = new StringBuilder();
                    buf.Length = 0;
                    buf.Append(SmtpCommands.Mail);
                    buf.Append("<");
                    buf.Append(MailMessage.From);
                    buf.Append(">");
                    _con.SendCommand(buf.ToString());
                    _con.GetReply(out response, out code);
                    buf.Length = 0;
                    //set up list of to addresses
                    foreach (MailAddress recipient in MailMessage.To)
                    {
                        buf.Append(SmtpCommands.Recipient);
                        buf.Append("<");

                        buf.Append(recipient);
                        buf.Append(">");
                        _con.SendCommand(buf.ToString());
                        _con.GetReply(out response, out code);
                        buf.Length = 0;
                    }
                    //set up list of cc addresses
                    buf.Length = 0;
                    foreach (MailAddress recipient in MailMessage.CC)
                    {
                        buf.Append(SmtpCommands.Recipient);
                        buf.Append("<");
                        buf.Append(recipient);
                        buf.Append(">");
                        _con.SendCommand(buf.ToString());
                        _con.GetReply(out response, out code);
                        buf.Length = 0;
                    }
                    //set up list of bcc addresses
                    buf.Length = 0;
                    foreach (MailAddress o in MailMessage.Bcc)
                    {
                        buf.Append(SmtpCommands.Recipient);
                        buf.Append("<");
                        buf.Append(o);
                        buf.Append(">");
                        _con.SendCommand(buf.ToString());
                        _con.GetReply(out response, out code);
                        buf.Length = 0;
                    }
                    buf.Length = 0;
                    //set headers
                    _con.SendCommand(SmtpCommands.Data);
                    _con.SendCommand("X-Mailer: AIM.MimeMailer");
                    DateTime today = DateTime.Now;
                    buf.Append(SmtpCommands.Date);
                    buf.Append(today.ToLongDateString());
                    _con.SendCommand(buf.ToString());
                    buf.Length = 0;
                    buf.Append(SmtpCommands.From);
                    buf.Append(MailMessage.From);
                    _con.SendCommand(buf.ToString());
                    buf.Length = 0;
                    buf.Append(SmtpCommands.To);
                    buf.Append(MailMessage.To[0]);
                    for (int x = 1; x < MailMessage.To.Count; ++x)
                    {
                        buf.Append(";");
                        buf.Append(MailMessage.To[x]);
                    }
                    _con.SendCommand(buf.ToString());
                    if (MailMessage.CC.Count > 0)
                    {
                        buf.Length = 0;
                        buf.Append(SmtpCommands.Cc);
                        buf.Append(MailMessage.CC[0]);
                        for (int x = 1; x < MailMessage.CC.Count; ++x)
                        {
                            buf.Append(";");
                            buf.Append(MailMessage.CC[x]);
                        }
                        _con.SendCommand(buf.ToString());
                    }
                    if (MailMessage.Bcc.Count > 0)
                    {
                        buf.Length = 0;
                        buf.Append(SmtpCommands.Bcc);
                        buf.Append(MailMessage.Bcc[0]);
                        for (int x = 1; x < MailMessage.Bcc.Count; ++x)
                        {
                            buf.Append(";");
                            buf.Append(MailMessage.Bcc[x]);
                        }
                        _con.SendCommand(buf.ToString());
                    }
                    buf.Length = 0;
                    buf.Append(SmtpCommands.ReplyTo);
                    buf.Append(MailMessage.From);
                    _con.SendCommand(buf.ToString());
                    buf.Length = 0;
                    buf.Append(SmtpCommands.Subject);
                    buf.Append(MailMessage.Subject);
                    _con.SendCommand(buf.ToString());
                    buf.Length = 0;
                    //declare mime info for message
                    _con.SendCommand("MIME-Version: 1.0");
                    if (!_sendAsHtml ||
                        (_sendAsHtml && ((MimeAttachment.InlineCount > 0) || (MimeAttachment.AttachCount > 0))))
                    {
                        _con.SendCommand("Content-Type: multipart/mixed; boundary=\"#SEPERATOR1#\"\r\n");
                        _con.SendCommand("This is a multi-part message.\r\n\r\n--#SEPERATOR1#");
                    }
                    if (_sendAsHtml)
                    {
                        _con.SendCommand("Content-Type: multipart/related; boundary=\"#SEPERATOR2#\"");
                        _con.SendCommand("Content-Transfer-Encoding: quoted-printable\r\n");
                        _con.SendCommand("--#SEPERATOR2#");

                    }
                    if (_sendAsHtml && MimeAttachment.InlineCount > 0)
                    {
                        _con.SendCommand("Content-Type: multipart/alternative; boundary=\"#SEPERATOR3#\"");
                        _con.SendCommand("Content-Transfer-Encoding: quoted-printable\r\n");
                        _con.SendCommand("--#SEPERATOR3#");
                        _con.SendCommand("Content-Type: text/html; charset=iso-8859-1");
                        _con.SendCommand("Content-Transfer-Encoding: quoted-printable\r\n");
                        _con.SendCommand(BodyToQuotedPrintable());
                        _con.SendCommand("--#SEPERATOR3#");
                        _con.SendCommand("Content-Type: text/plain; charset=iso-8859-1");
                        _con.SendCommand(
                            "\r\nIf you can see this, then your email client does not support MHTML messages.");
                        _con.SendCommand("--#SEPERATOR3#--\r\n");
                        _con.SendCommand("--#SEPERATOR2#\r\n");
                        SendAttachments(buf, AttachmentLocation.Inline);
                    }
                    else
                    {
                        if (_sendAsHtml)
                        {
                            _con.SendCommand("Content-Type: text/html; charset=iso-8859-1");
                            _con.SendCommand("Content-Transfer-Encoding: quoted-printable\r\n");
                        }
                        else
                        {
                            _con.SendCommand("Content-Type: text/plain; charset=iso-8859-1");
                            _con.SendCommand("Content-Transfer-Encoding: quoted-printable\r\n");
                        }
                        _con.SendCommand(BodyToQuotedPrintable());
                    }
                    if (_sendAsHtml)
                    {
                        _con.SendCommand("\r\n--#SEPERATOR2#--");
                    }
                    if (MimeAttachment.AttachCount > 0)
                    {
                        //send normal attachments
                        SendAttachments(buf, AttachmentLocation.Attachmed);
                    }
                    //finish up message
                    _con.SendCommand("");
                    if (MimeAttachment.InlineCount > 0 || MimeAttachment.AttachCount > 0)
                    {
                        _con.SendCommand("--#SEPERATOR1#--");
                    }

                    _con.SendCommand(".");
                    _con.GetReply(out response, out code);

                    var replymessage = response;

                    _con.SendCommand(SmtpCommands.Quit);
                    _con.GetReply(out response, out code);
                    Console.WriteLine(response);
                    _con.Close();
                    InCall = false;

                    if (SendCompleted != null)
                    {
                        SendCompleted(this, new AsyncCompletedEventArgs(null, false, response));
                    }
                }
            }
        }

        public bool InCall { get; private set; }

        private bool AuthenticateAsPlainText(out string response, out int code)
        {
            _con.SendCommand(SmtpCommands.Auth + SmtpCommands.AuthLogin + Gap + SmtpCommands.AuthPlian);
            _con.GetReply(out response, out code);
            Console.Out.WriteLine("Reply to Plain 1: " + response + " Code :" + code);

            _con.SendCommand(_user);
            _con.GetReply(out response, out code);
            Console.Out.WriteLine("Reply to Plain 2: " + response + " Code :" + code);

            _con.SendCommand(_password);
            _con.GetReply(out response, out code);
            Console.Out.WriteLine("Reply to Plain 3: " + response + " Code :" + code);
            if (code == 235)
                return true;
            return false;
        
        }

        private bool AuthenticateAsBase64(out string response, out int code)
        {
            _con.SendCommand(SmtpCommands.Auth + SmtpCommands.AuthLogin);
            _con.GetReply(out response, out code);
            Console.Out.WriteLine("Reply to b64 1: " + response + " Code :" + code);

            _con.SendCommand(Convert.ToBase64String(Encoding.ASCII.GetBytes(_user)));
            _con.GetReply(out response, out code);
            Console.Out.WriteLine("Reply to b64 2: " + response + " Code :" + code);

            _con.SendCommand(Convert.ToBase64String(Encoding.ASCII.GetBytes(_password)));

            _con.GetReply(out response, out code);
            Console.Out.WriteLine("Reply to b64 3: " + response + " Code :" + code);

            if (code == 235)
                return true;
            return false;
        }

        private void ParseResponse(string response, int code)
        {
            
        }

        private void QuiteConnection(out string response, out int code)
        {
            _con.SendCommand(SmtpCommands.Quit);
            _con.GetReply(out response, out code);
            Console.WriteLine(response);
            _con.Close();
            InCall = false;
            _con = null;
        }

        /// <summary>
		/// Send the message on a seperate thread.
		/// </summary>
		public void SendMailAsync(AbstractMailMessage message = null)
		{
		    if (message == null)
		        message = this.MailMessage;
			new Thread(()=> SendMail(message)).Start();
		}

		/// <summary>
		/// Send any attachments.
		/// </summary>
		/// <param name="buf">String work area.</param>
		/// <param name="type">Attachment type to send.</param>
		private void SendAttachments(StringBuilder buf, AttachmentLocation type)
		{
            
			//declare mime info for attachment
			var fbuf = new byte[2048];
		    string seperator = type == AttachmentLocation.Attachmed ? "\r\n--#SEPERATOR1#" : "\r\n--#SEPERATOR2#";
			buf.Length = 0;
			foreach(MimeAttachment o in MailMessage.Attachments)
			{									
				MimeAttachment attachment = o;
				if(attachment.Location != type)
				{
					continue;
				}																			
				var cs = new CryptoStream(new FileStream(attachment.FileName, FileMode.Open, FileAccess.Read, FileShare.Read), new ToBase64Transform(), CryptoStreamMode.Read);
				_con.SendCommand(seperator);
				buf.Append("Content-Type: ");
				buf.Append(attachment.ContentType);
				buf.Append("; name=");
				buf.Append(Path.GetFileName(attachment.FileName));
				_con.SendCommand(buf.ToString());
				_con.SendCommand("Content-Transfer-Encoding: base64");
				buf.Length = 0;
				buf.Append("Content-Disposition: attachment; filename=");
				buf.Append(Path.GetFileName(attachment.FileName));
				_con.SendCommand(buf.ToString());
				buf.Length = 0;
				buf.Append("Content-ID: ");
				buf.Append(Path.GetFileNameWithoutExtension(attachment.FileName));				
				buf.Append("\r\n");
				_con.SendCommand(buf.ToString());								
				buf.Length = 0;
				int num = cs.Read(fbuf, 0, 2048);
				while(num > 0)
				{					
					_con.SendData(Encoding.ASCII.GetChars(fbuf, 0, num), 0, num);
					num = cs.Read(fbuf, 0, 2048);
				}
				cs.Close();
				_con.SendCommand("");
			}
		}

        #endregion
		
        /// <summary>
		/// Encode the body as in quoted-printable format.
		/// Adapted from PJ Naughter's quoted-printable encoding code.
		/// For more information see RFC 2045.
		/// </summary>
		/// <returns>The encoded body.</returns>
		private string BodyToQuotedPrintable()
		{
			var stringBuilder = new StringBuilder();
			sbyte currentByte;
			foreach (char t in MailMessage.Body)
			{
			    currentByte = (sbyte) t;
			    //is this a valid ascii character?
			    if( ((currentByte >= 33) && (currentByte <= 60)) || ((currentByte >= 62) && (currentByte <= 126)) || (currentByte == '\r') || (currentByte == '\n') || (currentByte == '\t') || (currentByte == ' '))
			    {
			        stringBuilder.Append(t);
			    }
			    else
			    {
			        stringBuilder.Append('=');
			        stringBuilder.Append(((sbyte)((currentByte & 0xF0) >> 4)).ToString("X"));
			        stringBuilder.Append(((sbyte) (currentByte & 0x0F)).ToString("X"));
			    }
			}
			//format data so that lines don't end with spaces (if so, add a trailing '='), etc.
			//for more detail see RFC 2045.
			int start = 0;
			string encodedString = stringBuilder.ToString();
			stringBuilder.Length = 0;
			for(int x = 0; x < encodedString.Length; ++x)
			{
				currentByte = (sbyte) encodedString[x];
				if(currentByte == '\n' || currentByte == '\r' || x == (encodedString.Length - 1))
				{
					stringBuilder.Append(encodedString.Substring(start, x - start + 1));
					start = x + 1;
					continue;
				}
				if((x - start) > 76)
				{
					bool inWord = true;
					while(inWord)
					{
						inWord = (!char.IsWhiteSpace(encodedString, x) && encodedString[x-2] != '=');
						if(inWord)
						{
							--x;
//							currentByte = (sbyte) encodedString[x];
						}
						if(x == start)
						{
							x = start + 76;
							break;
						}
					}
					stringBuilder.Append(encodedString.Substring(start, x - start + 1));
					stringBuilder.Append("=\r\n");
					start = x + 1;
				}
			}
			return stringBuilder.ToString();
		}

        public void Dispose()   
        {
            if (_con.Connected)
            {
                _con.Close();
            }
            _mailMessage.Dispose();
        }

        internal enum SupportedAuth
        {
            None = 0,
            Login = 1,
            NTLM = 2,
            GSSAPI = 4,
            WDigest = 8,
        }

        internal void ParseExtensions(string[] extensions)
        {
            int sizeOfAuthExtension = AuthExtension.Length;
               
            var supportedAuth = SupportedAuth.None;
            foreach (string extension in extensions)
            {
                var realextension = extension;
                if (realextension.Length>3)
                realextension = extension.Substring(4);
                Console.Out.WriteLine("Extenstion :" + extension);
                if (String.Compare(realextension, 0, AuthExtension, 0,
                    sizeOfAuthExtension, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    // remove the AUTH text including the following character 
                    // to ensure that split only gets the modules supported
                    string[] authTypes =
                        realextension.Remove(0, sizeOfAuthExtension).Split(new char[] { ' ', '=' },
                        StringSplitOptions.RemoveEmptyEntries);
                    foreach (string authType in authTypes)
                    {
                        if (String.Compare(authType, SmtpCommands.AuthLogin, StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            supportedAuth |= SupportedAuth.Login;
                        }
#if !FEATURE_PAL
                        else if (String.Compare(authType, AuthNtlm, StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            supportedAuth |= SupportedAuth.NTLM;
                        }
                        else if (String.Compare(authType, AuthGssapi, StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            supportedAuth |= SupportedAuth.GSSAPI;
                        }
                        else if (String.Compare(authType, AuthWDigest, StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            supportedAuth |= SupportedAuth.WDigest;
                        }
#endif // FEATURE_PAL
                    }
                }
                else if (String.Compare(realextension, 0, "dsn ", 0, 3, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    DsnEnabled = true;
                }
                else if (String.Compare(realextension, 0, SmtpCommands.StartTls, 0, 8, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    SupportsTls = true;
                }
                else if (String.Compare(realextension, 0, SmtpCommands.Utf8, 0, 8, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    ServerSupportsEai = true;
                }
            }
        }

    }
}
