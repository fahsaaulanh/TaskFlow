const { randomBytes } = require('node:crypto');
const { once } = require('node:events');
const { spawn, spawnSync } = require('node:child_process');
const path = require('node:path');
const { setTimeout: delay } = require('node:timers/promises');

const root = path.resolve(__dirname, '..');
const api = 'backend/TaskFlow.Api';
const infrastructure = 'backend/TaskFlow.Infrastructure';
const database = 'TaskFlowTest_' + randomBytes(16).toString('hex');
const password = 'Demo_!' + randomBytes(16).toString('hex');
let child;
let migrationAttempted = false;

function run(command, args, options = {}) {
  const result = spawnSync(command, args, { cwd: root, encoding: 'utf8', stdio: 'inherit', ...options });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`${command} ${args[0]} failed (${result.status ?? result.signal}).`);
  return result.stdout;
}

function quote(value) { return '"' + String(value).replaceAll('"', '""') + '"'; }

async function start() {
  const config = run('docker', ['compose', 'config', '--format', 'json'], { stdio: ['ignore', 'pipe', 'inherit'] });
  const sql = JSON.parse(config).services.sqlserver;
  const port = sql.ports.find((binding) => binding.target === 1433)?.published;
  if (!port || !sql.environment.MSSQL_SA_PASSWORD) throw new Error('Set up and start the local SQL Server Compose service first.');

  const env = {
    ...process.env,
    DOTNET_ENVIRONMENT: 'Development',
    ASPNETCORE_ENVIRONMENT: 'Development',
    Logging__LogLevel__Microsoft: 'Warning',
    'Logging__LogLevel__Microsoft.Hosting.Lifetime': 'Information',
    TaskFlow__DemoPassword: password,
    ConnectionStrings__TaskFlow: `Server=127.0.0.1,${port};Database=${database};User ID=sa;Password=${quote(sql.environment.MSSQL_SA_PASSWORD)};Encrypt=True;TrustServerCertificate=True`,
  };
  run('dotnet', ['tool', 'restore'], { env });
  run('dotnet', ['build', api, '--configuration', 'Release'], { env });
  migrationAttempted = true;
  run('dotnet', ['ef', 'database', 'update', '--project', infrastructure, '--startup-project', api,
    '--configuration', 'Release', '--no-build'], { env });
  const dll = `${api}/bin/Release/net10.0/TaskFlow.Api.dll`;
  run('dotnet', [dll, '--seed-demo'], { env });

  child = spawn('dotnet', [dll, '--urls', 'http://127.0.0.1:0'], { cwd: root, env, stdio: ['ignore', 'pipe', 'pipe'] });
  let output = '';
  let errors = '';
  child.stdout.on('data', (buffer) => { output = (output + buffer).slice(-8000); });
  child.stderr.on('data', (buffer) => { errors = (errors + buffer).slice(-8000); });
  let spawnError;
  child.once('error', (error) => { spawnError = error; });
  const deadline = Date.now() + 30000;
  while (Date.now() < deadline && child.exitCode === null && child.signalCode === null && !spawnError) {
    const origin = output.match(/Now listening on: (http:\/\/127\.0\.0\.1:\d+)/)?.[1];
    if (origin) return { origin, password };
    await delay(150);
  }
  throw spawnError ?? new Error(`TaskFlow API did not start.\n${output}\n${errors}`);
}

async function stop() {
  if (child?.pid && child.exitCode === null && child.signalCode === null) {
    const exited = once(child, 'exit');
    child.kill();
    await exited;
  }
  if (migrationAttempted && /^TaskFlowTest_[0-9a-f]{32}$/.test(database)) {
    // The random database above is reserved for this run. Other databases and the Compose volume stay in place.
    const statement = `IF DB_ID(N'${database}') IS NOT NULL BEGIN
      ALTER DATABASE [${database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
      DROP DATABASE [${database}];
    END;
    GO\n`;
    run('docker', ['compose', 'exec', '-T', 'sqlserver', 'sh', '-c',
      'export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"; exec /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -l 10'],
    { input: statement, stdio: ['pipe', 'inherit', 'inherit'] });
  }
}

module.exports = { start, stop };
