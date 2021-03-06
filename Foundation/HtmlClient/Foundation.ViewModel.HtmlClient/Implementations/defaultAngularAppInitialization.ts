﻿/// <reference path="../../foundation.core.htmlclient/foundation.core.d.ts" />
module Foundation.ViewModel.Implementations {

    let dependencyManager = Core.DependencyManager.getCurrent();

    export class DefaultAngularAppInitialization implements Core.Contracts.IAppEvents {

        public constructor(public pathProvider = dependencyManager.resolveObject<Contracts.IPathProvider>("PathProvider"), public dateTimeService = dependencyManager.resolveObject<Contracts.IDateTimeService>("DateTimeService"), public angularConfigs = dependencyManager.resolveAllObjects<Contracts.IAngularConfiguration>("AngularConfiguration"), public securityService = dependencyManager.resolveObject<Core.Contracts.ISecurityService>("SecurityService"), public logger = dependencyManager.resolveObject<Core.Contracts.ILogger>("Logger"), public clientAppProfileManager = dependencyManager.resolveObject<Core.ClientAppProfileManager>("ClientAppProfileManager")) {
            this.clientAppProfile = clientAppProfileManager.getClientAppProfile();
        }

        private clientAppProfile: Core.Contracts.IClientAppProfile;

        protected getBaseModuleDependencies(): Array<string> {
            return [];
        }

        protected async onAppRun(app: ng.IModule): Promise<void> {

        }

        protected async registerValues(app: ng.IModule): Promise<void> {
            app.value("$routerRootComponent", "app");
        }

        protected async registerComponents(app: ng.IModule): Promise<void> {

            const dependencyManager = Core.DependencyManager.getCurrent();

            const formViewModelDependencies = dependencyManager.getAllFormViewModelsDependencies();

            formViewModelDependencies.forEach(vm => {

                const original$routerOnActivate = vm.type.prototype.$routerOnActivate;

                const original$routerCanActivate = vm.type.prototype.$routerCanActivate;

                vm.type.prototype.$routerOnActivate = async function (prev, next) {

                    const canActivate = original$routerCanActivate == null || await original$routerCanActivate.call(this, [next, prev]);

                    if (canActivate == false)
                        throw new Error("Can't activate view model");

                    if (original$routerOnActivate != null) {
                        return await original$routerOnActivate.call(this, [next, prev]);
                    }
                };

            });

            formViewModelDependencies.forEach(vm => {

                if (vm.templateUrl != null)
                    vm.templateUrl = this.pathProvider.getFullPath(vm.templateUrl);

                vm.controllerAs = vm.controllerAs || "vm";

                vm.bindings = angular.extend(vm.bindings || {}, { $router: "<" });

                if (vm.name != "app") {
                    vm.require = angular.extend(vm.require || {}, { ngOutlet: "^ngOutlet" });
                }

                app.component(vm.name, vm);

            });

            dependencyManager.getAllComponentDependencies().forEach(component => {

                component.controllerAs = component.controllerAs || "vm";

                if (component.templateUrl != null)
                    component.templateUrl = this.pathProvider.getFullPath(component.templateUrl);

                app.component(component.name, component);

            });
        }

        protected async buildAppModule(): Promise<ng.IModule> {

            const baseModuleDependencies = this.getBaseModuleDependencies();

            const app = angular.module(this.clientAppProfile.appName, baseModuleDependencies);

            return app;
        }

        protected async configureAppModule(app: ng.IModule): Promise<void> {

            const logger = this.logger;

            function extendExceptionHandler($delegate) {

                return (exception, cause) => {

                    $delegate(exception, cause);

                    logger.logError(exception.message || exception, cause, exception);
                };
            }

            app.config(["$provide", $provide => {
                $provide.decorator("$exceptionHandler",
                    ["$delegate", extendExceptionHandler]);
            }]);

            app.config(["$httpProvider", ($httpProvider: ng.IHttpProvider) => {

                $httpProvider.useApplyAsync(true);

            }]);

            app.config(["$compileProvider", ($compileProvider: ng.ICompileProvider) => {
                $compileProvider.debugInfoEnabled(this.clientAppProfile.isDebugMode);
                $compileProvider.commentDirectivesEnabled(false);
                $compileProvider.cssClassDirectivesEnabled(false);
            }]);

            app.config(["$logProvider", ($logProvider: ng.ILogProvider) => {
                $logProvider.debugEnabled(this.clientAppProfile.isDebugMode);
            }]);

            if (typeof ngMaterial != "undefined") {

                app.decorator("mdSwitchDirective", ["$delegate", function mdSwitchDecorator($delegate: ng.ISCEDelegateService) {

                    const directive = ($delegate[0] as ng.IDirective);

                    const originalCompile = directive.compile;

                    directive.compile = function (element, attr) {

                        const result = originalCompile.apply(this, arguments);

                        const mdInputContainerParent = element.parent("md-input-container");

                        if (mdInputContainerParent.length != 0) {

                            mdInputContainerParent.addClass("md-input-has-value");

                        }

                        return result;
                    }

                    return $delegate;
                }]);

            }

            for (let angularConfig of this.angularConfigs) {
                await angularConfig.configure(app);
            }
        }

        protected async registerDirectives(app: ng.IModule): Promise<void> {

            const dependencyManager = Core.DependencyManager.getCurrent();

            const pathProvider = this.pathProvider;

            dependencyManager.getAllDirectivesDependencies()
                .map(d => { return { name: d.name, instance: Reflect.construct(d.type as Function, []) as Contracts.IDirective }; })
                .forEach(directive => {

                    const originalGetDirectiveFactory = directive.instance.getDirectiveFactory();

                    const modifiedGetDirectiveFactory = function () {

                        const directiveResult: ng.IDirective = originalGetDirectiveFactory.apply(this, arguments);

                        if (directiveResult.templateUrl != null) {
                            directiveResult.templateUrl = pathProvider.getFullPath(directiveResult.templateUrl);
                        }

                        return directiveResult;

                    };

                    app.directive(directive.name, modifiedGetDirectiveFactory);
                });
        }

        protected async registerFilters(app: ng.IModule): Promise<void> {

            const dateTimeService = this.dateTimeService;
            const pathProvider = this.pathProvider;

            app.filter("bitDate", () => {

                return function (date: Date): string {

                    return dateTimeService.getFormattedDate(date);

                }
            });

            app.filter("bitDateTime", () => {

                return function (date: Date): string {

                    return dateTimeService.getFormattedDateTime(date);

                }
            });

            app.filter("trusted", ["$sce", function ($sce: ng.ISCEService) {
                return function (url) {
                    return $sce.trustAsResourceUrl(url);
                };
            }]);

            app.filter("files", () => {

                return (path: string): string => {

                    return pathProvider.getFullPath(path);

                }

            });
        }

        @Core.Log()
        public async onAppStartup(): Promise<void> {

            return new Promise<void>((res, rej) => {

                angular.element(document.body).ready(async () => {

                    try {

                        const app = await this.buildAppModule();

                        Core.DependencyManager.getCurrent().registerCustomObjectResolver({
                            canResolve: (name) => angular.element(document.body).injector().has(name),
                            resolve: <T>(name) => angular.element(document.body).injector().get<T>(name)
                        });

                        await this.registerValues(app);

                        await this.configureAppModule(app);

                        await this.registerComponents(app);

                        await this.registerFilters(app);

                        await this.registerDirectives(app);

                        await this.onAppRun(app);

                        angular.bootstrap(document.body, [this.clientAppProfile.appName], {
                            strictDi: true
                        });

                        res();
                    }
                    catch (e) {
                        rej(e);
                        throw e;
                    }

                });

            });
        }
    }
}