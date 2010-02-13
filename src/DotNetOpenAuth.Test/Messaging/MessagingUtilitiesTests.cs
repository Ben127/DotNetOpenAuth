﻿//-----------------------------------------------------------------------
// <copyright file="MessagingUtilitiesTests.cs" company="Andrew Arnott">
//     Copyright (c) Andrew Arnott. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace DotNetOpenAuth.Test.Messaging
{
	using System;
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.IO;
	using System.Net;
	using System.Text.RegularExpressions;
	using System.Web;
	using DotNetOpenAuth.Messaging;
	using DotNetOpenAuth.Test.Mocks;
	using NUnit.Framework;

	[TestFixture]
	public class MessagingUtilitiesTests : TestBase {
		[TestCase]
		public void CreateQueryString() {
			var args = new Dictionary<string, string>();
			args.Add("a", "b");
			args.Add("c/d", "e/f");
			Assert.AreEqual("a=b&c%2Fd=e%2Ff", MessagingUtilities.CreateQueryString(args));
		}

		[TestCase]
		public void CreateQueryStringEmptyCollection() {
			Assert.AreEqual(0, MessagingUtilities.CreateQueryString(new Dictionary<string, string>()).Length);
		}

		[TestCase, ExpectedException(typeof(ArgumentNullException))]
		public void CreateQueryStringNullDictionary() {
			MessagingUtilities.CreateQueryString(null);
		}

		[TestCase]
		public void AppendQueryArgs() {
			UriBuilder uri = new UriBuilder("http://baseline.org/page");
			var args = new Dictionary<string, string>();
			args.Add("a", "b");
			args.Add("c/d", "e/f");
			MessagingUtilities.AppendQueryArgs(uri, args);
			Assert.AreEqual("http://baseline.org/page?a=b&c%2Fd=e%2Ff", uri.Uri.AbsoluteUri);
			args.Clear();
			args.Add("g", "h");
			MessagingUtilities.AppendQueryArgs(uri, args);
			Assert.AreEqual("http://baseline.org/page?a=b&c%2Fd=e%2Ff&g=h", uri.Uri.AbsoluteUri);
		}

		[TestCase, ExpectedException(typeof(ArgumentNullException))]
		public void AppendQueryArgsNullUriBuilder() {
			MessagingUtilities.AppendQueryArgs(null, new Dictionary<string, string>());
		}

		[TestCase]
		public void AppendQueryArgsNullDictionary() {
			MessagingUtilities.AppendQueryArgs(new UriBuilder(), null);
		}

		[TestCase]
		public void ToDictionary() {
			NameValueCollection nvc = new NameValueCollection();
			nvc["a"] = "b";
			nvc["c"] = "d";
			nvc[string.Empty] = "emptykey";
			Dictionary<string, string> actual = MessagingUtilities.ToDictionary(nvc);
			Assert.AreEqual(nvc.Count, actual.Count);
			Assert.AreEqual(nvc["a"], actual["a"]);
			Assert.AreEqual(nvc["c"], actual["c"]);
		}

		[TestCase, ExpectedException(typeof(ArgumentException))]
		public void ToDictionaryWithNullKey() {
			NameValueCollection nvc = new NameValueCollection();
			nvc[null] = "a";
			nvc["b"] = "c";
			nvc.ToDictionary(true);
		}

		[TestCase]
		public void ToDictionaryWithSkippedNullKey() {
			NameValueCollection nvc = new NameValueCollection();
			nvc[null] = "a";
			nvc["b"] = "c";
			var dictionary = nvc.ToDictionary(false);
			Assert.AreEqual(1, dictionary.Count);
			Assert.AreEqual(nvc["b"], dictionary["b"]);
		}

		[TestCase]
		public void ToDictionaryNull() {
			Assert.IsNull(MessagingUtilities.ToDictionary(null));
		}

		[TestCase, ExpectedException(typeof(ArgumentNullException))]
		public void ApplyHeadersToResponseNullAspNetResponse() {
			MessagingUtilities.ApplyHeadersToResponse(new WebHeaderCollection(), (HttpResponse)null);
		}

		[TestCase, ExpectedException(typeof(ArgumentNullException))]
		public void ApplyHeadersToResponseNullListenerResponse() {
			MessagingUtilities.ApplyHeadersToResponse(new WebHeaderCollection(), (HttpListenerResponse)null);
		}

		[TestCase, ExpectedException(typeof(ArgumentNullException))]
		public void ApplyHeadersToResponseNullHeaders() {
			MessagingUtilities.ApplyHeadersToResponse(null, new HttpResponse(new StringWriter()));
		}

		[TestCase]
		public void ApplyHeadersToResponse() {
			var headers = new WebHeaderCollection();
			headers[HttpResponseHeader.ContentType] = "application/binary";

			var response = new HttpResponse(new StringWriter());
			MessagingUtilities.ApplyHeadersToResponse(headers, response);

			Assert.AreEqual(headers[HttpResponseHeader.ContentType], response.ContentType);
		}

		/// <summary>
		/// Verifies RFC 3986 compliant URI escaping, as required by the OpenID and OAuth specifications.
		/// </summary>
		/// <remarks>
		/// The tests in this method come from http://wiki.oauth.net/TestCases
		/// </remarks>
		[TestCase]
		public void EscapeUriDataStringRfc3986Tests() {
			Assert.AreEqual("abcABC123", MessagingUtilities.EscapeUriDataStringRfc3986("abcABC123"));
			Assert.AreEqual("-._~", MessagingUtilities.EscapeUriDataStringRfc3986("-._~"));
			Assert.AreEqual("%25", MessagingUtilities.EscapeUriDataStringRfc3986("%"));
			Assert.AreEqual("%2B", MessagingUtilities.EscapeUriDataStringRfc3986("+"));
			Assert.AreEqual("%26%3D%2A", MessagingUtilities.EscapeUriDataStringRfc3986("&=*"));
			Assert.AreEqual("%0A", MessagingUtilities.EscapeUriDataStringRfc3986("\n"));
			Assert.AreEqual("%20", MessagingUtilities.EscapeUriDataStringRfc3986(" "));
			Assert.AreEqual("%7F", MessagingUtilities.EscapeUriDataStringRfc3986("\u007f"));
			Assert.AreEqual("%C2%80", MessagingUtilities.EscapeUriDataStringRfc3986("\u0080"));
			Assert.AreEqual("%E3%80%81", MessagingUtilities.EscapeUriDataStringRfc3986("\u3001"));
		}

		/// <summary>
		/// Verifies the overall format of the multipart POST is correct.
		/// </summary>
		[TestCase]
		public void PostMultipart() {
			var httpHandler = new TestWebRequestHandler();
			bool callbackTriggered = false;
			httpHandler.Callback = req => {
				Match m = Regex.Match(req.ContentType, "multipart/form-data; boundary=(.+)");
				Assert.IsTrue(m.Success, "Content-Type HTTP header not set correctly.");
				string boundary = m.Groups[1].Value;
				boundary = boundary.Substring(0, boundary.IndexOf(';')); // trim off charset
				string expectedEntity = "--{0}\r\nContent-Disposition: form-data; name=\"a\"\r\n\r\nb\r\n--{0}--\r\n";
				expectedEntity = string.Format(expectedEntity, boundary);
				string actualEntity = httpHandler.RequestEntityAsString;
				Assert.AreEqual(expectedEntity, actualEntity);
				callbackTriggered = true;
				Assert.AreEqual(req.ContentLength, actualEntity.Length);
				IncomingWebResponse resp = new CachedDirectWebResponse();
				return resp;
			};
			var request = (HttpWebRequest)WebRequest.Create("http://someserver");
			var parts = new[] {
				MultipartPostPart.CreateFormPart("a", "b"),
			};
			request.PostMultipart(httpHandler, parts);
			Assert.IsTrue(callbackTriggered);
		}

		/// <summary>
		/// Verifies proper behavior of GetHttpVerb
		/// </summary>
		[TestCase]
		public void GetHttpVerbTest() {
			Assert.AreEqual("GET", MessagingUtilities.GetHttpVerb(HttpDeliveryMethods.GetRequest));
			Assert.AreEqual("POST", MessagingUtilities.GetHttpVerb(HttpDeliveryMethods.PostRequest));
			Assert.AreEqual("HEAD", MessagingUtilities.GetHttpVerb(HttpDeliveryMethods.HeadRequest));
			Assert.AreEqual("DELETE", MessagingUtilities.GetHttpVerb(HttpDeliveryMethods.DeleteRequest));
			Assert.AreEqual("PUT", MessagingUtilities.GetHttpVerb(HttpDeliveryMethods.PutRequest));

			Assert.AreEqual("GET", MessagingUtilities.GetHttpVerb(HttpDeliveryMethods.GetRequest | HttpDeliveryMethods.AuthorizationHeaderRequest));
			Assert.AreEqual("POST", MessagingUtilities.GetHttpVerb(HttpDeliveryMethods.PostRequest | HttpDeliveryMethods.AuthorizationHeaderRequest));
			Assert.AreEqual("HEAD", MessagingUtilities.GetHttpVerb(HttpDeliveryMethods.HeadRequest | HttpDeliveryMethods.AuthorizationHeaderRequest));
			Assert.AreEqual("DELETE", MessagingUtilities.GetHttpVerb(HttpDeliveryMethods.DeleteRequest | HttpDeliveryMethods.AuthorizationHeaderRequest));
			Assert.AreEqual("PUT", MessagingUtilities.GetHttpVerb(HttpDeliveryMethods.PutRequest | HttpDeliveryMethods.AuthorizationHeaderRequest));
		}

		/// <summary>
		/// Verifies proper behavior of GetHttpVerb on invalid input.
		/// </summary>
		[TestCase, ExpectedException(typeof(ArgumentException))]
		public void GetHttpVerbOutOfRangeTest() {
			MessagingUtilities.GetHttpVerb(HttpDeliveryMethods.PutRequest | HttpDeliveryMethods.PostRequest);
		}

		/// <summary>
		/// Verifies proper behavior of GetHttpDeliveryMethod
		/// </summary>
		[TestCase]
		public void GetHttpDeliveryMethodTest() {
			Assert.AreEqual(HttpDeliveryMethods.GetRequest, MessagingUtilities.GetHttpDeliveryMethod("GET"));
			Assert.AreEqual(HttpDeliveryMethods.PostRequest, MessagingUtilities.GetHttpDeliveryMethod("POST"));
			Assert.AreEqual(HttpDeliveryMethods.HeadRequest, MessagingUtilities.GetHttpDeliveryMethod("HEAD"));
			Assert.AreEqual(HttpDeliveryMethods.PutRequest, MessagingUtilities.GetHttpDeliveryMethod("PUT"));
			Assert.AreEqual(HttpDeliveryMethods.DeleteRequest, MessagingUtilities.GetHttpDeliveryMethod("DELETE"));
		}

		/// <summary>
		/// Verifies proper behavior of GetHttpDeliveryMethod for an unexpected input
		/// </summary>
		[TestCase, ExpectedException(typeof(ArgumentException))]
		public void GetHttpDeliveryMethodOutOfRangeTest() {
			MessagingUtilities.GetHttpDeliveryMethod("UNRECOGNIZED");
		}
	}
}
