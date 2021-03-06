﻿using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Webpack.AspNetCore.Static.Internal
{
    internal class ManifestStorageService
    {
        private readonly StaticContext context;
        private readonly PhysicalFileManifestReader reader;
        private readonly ManifestStorage storage;
        private readonly ManifestMonitor monitor;
        private readonly ILogger<ManifestStorageService> logger;

        public ManifestStorageService(
            StaticContext context,
            PhysicalFileManifestReader reader,
            ManifestStorage storage,
            ManifestMonitor monitor,
            ILogger<ManifestStorageService> logger)
        {
            this.context = context ??
                throw new ArgumentNullException(nameof(context));

            this.reader = reader ??
                throw new ArgumentNullException(nameof(reader));

            this.storage = storage ??
                throw new ArgumentNullException(nameof(storage));

            this.monitor = monitor ??
                throw new ArgumentNullException(nameof(monitor));

            this.logger = logger ??
                throw new ArgumentNullException(nameof(logger));
        }

        public void Start()
        {
            setupStorage().Wait();

            // run a background job which checks the manifest file
            // for changes and updates the storage
            Task.Run(async () =>
            {
                while (true) if (await monitor.WaitForChangesAsync()) await updateStorage();
            });

            async Task setupStorage()
            {
                var manifest = await reader.ReadAsync();

                // if we've failed to retrieve asset manifest
                // in the very beginning, on the application
                // startup, then there is no way to go further
                // so we're throwing the exception
                if (manifest == null)
                {
                    var message = "Failed to retrieve webpack asset manifest. " +
                        $"File path: '{context.ManifestPhysicalPath}'. " +
                        "Check out the file exists and it's a valid json asset manifest";

                    logger.LogError(message);

                    throw new WebpackException(message);
                }

                // set up the manifest storage
                storage.Setup(manifest);
                logger.LogInformation(
                    "Webpack asset manifest storage has been set up. " +
                    $"Keys: ({keysFormatted(manifest.Keys)})"
                );
            }

            async Task updateStorage()
            {
                var manifest = await reader.ReadAsync();

                // it's normal if we are failed to read the manifest
                // during the file updates. In this case we're keeping
                // the storage untouched and waiting for the next update
                if (manifest == null)
                {
                    logger.LogDebug($"Webpack asset manifest storage can not be updated now");
                    return;
                }

                // update the storage
                var updatedKeys = storage.Update(manifest);

                // log if we have any updated or added records
                if (updatedKeys.Any())
                {
                    logger.LogInformation(
                        "Webpack asset manifest storage has been updated. " +
                        $"Updated keys: ({keysFormatted(updatedKeys)})"
                    );
                }
            }

            string keysFormatted(IEnumerable<string> keys) => string.Join(" ", keys);
        }

    }
}
