using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IdentityServer3.Shaolinq.DataModel;
using IdentityServer3.Shaolinq.Stores;
using log4net;
using log4net.Config;
using Npgsql;
using Npgsql.Logging;
using NUnit.Framework;
using Shaolinq;
using Shaolinq.Sqlite;

namespace IdentityServer3.Shaolinq.Tests
{
	[TestFixture]
    public class DataAccessModelTests
    {
		private static readonly ILog Log = LogManager.GetLogger(typeof(DataAccessModelTests).Name);

		[TestFixtureSetUp]
		public void TestFixtureSetUp()
		{
			XmlConfigurator.Configure();
		}

		[Test]
		public void TestCreateDataAccessModel()
		{
			var dataModel = DataAccessModel.BuildDataAccessModel<IdentityServerDataAccessModel>(SqliteConfiguration.Create(":memory:", null));

			dataModel.Create(DatabaseCreationOptions.DeleteExistingDatabase);
		}


		[Test]
		public void StressTestClientStore()
		{
			var dataModel = DataAccessModel.BuildDataAccessModel<IdentityServerDataAccessModel>();

			var clientStore = new ClientStore(dataModel);

			const string TestClientId = "A06D44F1-027B-4EB7-9627-0F40D2A97300";

			const int numThreads = 2;

			var isRunning = true;

			for (var i = 0; i < numThreads; i++)
			{
				var dispatchThread = new Thread(_ =>
				{
					while (isRunning)
					{
						var result = clientStore.FindClientByIdAsync(TestClientId).Result;
						Assert.That(result.ClientId, Is.EqualTo(TestClientId));

						Log.Debug(result.ClientId);
					}
				})
				{ Name = $"Messaging dispatch thread: {i + 1}" };

				dispatchThread.Start();
			}

			Thread.Sleep(100000);
			isRunning = false;
		}

		[Test]
		public void StressTest()
		{
			NpgsqlLogManager.Provider = new ConsoleLoggingProvider(NpgsqlLogLevel.Info, true, true);

			var dataModel = DataAccessModel.BuildDataAccessModel<IdentityServerDataAccessModel>();

			Log.Debug(dataModel.GetCurrentSqlDatabaseContext().ConnectionString);

			const string testClientId = "A06D44F1-027B-4EB7-9627-0F40D2A97300";

			const int numThreads = 4;

			var cancellationTokenSource = new CancellationTokenSource();

			var resetEvents = new List<WaitHandle>();

			for (var i = 0; i < numThreads; i++)
			{
				var resetEvent = new ManualResetEvent(false);
				resetEvents.Add(resetEvent);

				var dispatchThread = new Thread(_ =>
				{
					while (!cancellationTokenSource.Token.IsCancellationRequested)
					{
						try
						{
							var result = dataModel.Clients.First(x => x.Id == testClientId);

							Assert.That(result.Id, Is.EqualTo(testClientId));

							Log.Debug(result.Id);
						}
						catch (Exception ex)
						{
							Log.Error("Test error", ex);
						}
					}

					Log.Debug("Stopped");
					resetEvent.Set();
				})
				{ Name = $"Thread: {i + 1}" };

				dispatchThread.Start();
			}

			Thread.Sleep(2000);

			cancellationTokenSource.Cancel();

			WaitHandle.WaitAll(resetEvents.ToArray());
		}

		[Test]
		public void ShaolinqTestQuery()
		{
			const string testClientId = "A06D44F1-027B-4EB7-9627-0F40D2A97300";

			var dataModel = DataAccessModel.BuildDataAccessModel<IdentityServerDataAccessModel>();

			var result = dataModel.Clients.First(x => x.Id == testClientId);
			Log.Debug(result.Id);

			Assert.That(result.Id, Is.EqualTo(testClientId));
		}

		[Test]
		public void StressTestNpgsql()
		{
			NpgsqlLogManager.Provider = new ConsoleLoggingProvider(NpgsqlLogLevel.Info, true, true);

			const int numThreads = 4;

			var cancellationTokenSource = new CancellationTokenSource();

			var resetEvents = new List<WaitHandle>();

			for (var i = 0; i < numThreads; i++)
			{
				var resetEvent = new ManualResetEvent(false);
				resetEvents.Add(resetEvent);

				var dispatchThread = new Thread(_ =>
				{
					while (!cancellationTokenSource.Token.IsCancellationRequested)
					{
						try
						{
							this.NpgSqlTestQuery();
						}
						catch (Exception ex)
						{
							Log.Error("Test error", ex);
						}
					}

					Log.Debug("Stopped");
					resetEvent.Set();
				})
				{ Name = $"Thread: {i + 1}" };

				dispatchThread.Start();
			}

			Thread.Sleep(2000);

			cancellationTokenSource.Cancel();

			WaitHandle.WaitAll(resetEvents.ToArray());
		}

		private void NpgSqlTestQuery()
		{
			const string connectionString = "db.dev.ldn.kastr.tv;Username=postgres;Password=postgres;Port=6432;Pooling=True;Enlist=False;Minimum Pool Size=0;Maximum Pool Size=2;Keepalive=3;Connection Idle Lifetime=3;Convert Infinity";
			//const string connectionString = "Host=localhost;Username=postgres;Password=postgres;Port=5432;Pooling=True;Enlist=False;Minimum Pool Size=0;Maximum Pool Size=2;Database=kastrauth";
			const string testClientId = "A06D44F1-027B-4EB7-9627-0F40D2A97300";
			const string queryString = "SELECT * FROM \"Client\" where \"ClientId\" = @clientId";

			using (var conn = NpgsqlFactory.Instance.CreateConnection())
			{
				conn.ConnectionString = connectionString;
				conn.Open();

				using (var cmd = conn.CreateCommand())
				{
					cmd.CommandText = queryString;
					var clientIdParam = cmd.CreateParameter();
					clientIdParam.ParameterName = "@clientId";
					clientIdParam.Value = testClientId;
					cmd.Parameters.Add(clientIdParam);

					using (var reader = cmd.ExecuteReader())
					{
						var ord = reader.GetOrdinal("ClientId");

						while (reader.Read())
						{
							var clientId = reader.GetString(ord);
							Assert.That(clientId, Is.EqualTo(testClientId));

							Log.Debug(clientId);
						}
					}
				}
			}
		}
	}
}
