// App_Plugins/ApprovalWorkflow/approval.controller.js
angular.module('umbraco')
    .controller('ApprovalDashboardController', function ($scope, $http, userService, notificationsService, editorService, overlayService) {
        var vm = this;
        vm.pendingItems = [];
        vm.loading = false;
        vm.error = null;
        vm.currentUser = null;

        function init() {
            getCurrentUser().then(function () {
                loadPendingContent();
            });
        }

        function getCurrentUser() {
            return userService.getCurrentUser().then(function (user) {
                vm.currentUser = user;
            });
        }

        function loadPendingContent() {
            vm.loading = true;
            vm.error = null;

            var config = {
                headers: {
                    'X-Current-User-Id': vm.currentUser.id.toString(),
                    'X-Current-User-Email': vm.currentUser.email,
                    'X-Current-User-Name': vm.currentUser.name
                }
            };

            $http.get('/api/ContentApi/pending-approvals', config).then(function (response) {
                vm.pendingItems = response.data;
                vm.loading = false;
            }).catch(function (error) {
                vm.loading = false;
                vm.error = "Failed to load pending items: " + (error.data ? error.data.error : error.statusText);
                console.error("Error loading pending approvals:", error);
            });
        }

        vm.viewComparison = function (item) {
            overlayService.open({
                view: "/App_Plugins/ApprovalWorkflow/preview-dialog.html",
                title: "Preview Changes - " + item.name,
                size: "medium",
                position: "center",
                comparison: null,
                loading: true,
                contentId: item.id,
                controller: function ($scope, $http, $sce) {
                    // Load comparison data
                    $http.get('/api/ContentApi/content-comparison/' + $scope.model.contentId)
                        .then(function (response) {
                            $scope.model.comparison = response.data;
                            $scope.model.loading = false;
                        })
                        .catch(function (error) {
                            notificationsService.error("Error", "Failed to load comparison");
                            $scope.model.close();
                        });

                    // Trust HTML filter
                    $scope.trustAsHtml = function (html) {
                        return $sce.trustAsHtml(html);
                    };
                },
                close: function () {
                    overlayService.close();
                }
            });
        };

        vm.approve = function (item) {
            if (vm.loading || !vm.currentUser) return;

            // Show approval dialog with comment option
            overlayService.open({
                view: "/App_Plugins/ApprovalWorkflow/approval-dialog.html",
                title: "Approve Content",
                subtitle: item.name,
                submitButtonLabel: "Approve",
                submitButtonStyle: "success",
                closeButtonLabel: "Cancel",
                comment: "",
                submit: function (model) {
                    performApproval(item, model.comment);
                    overlayService.close();
                },
                close: function () {
                    overlayService.close();
                }
            });
        };

        function performApproval(item, comment) {
            vm.loading = true;

            $http.post('/api/ContentApi/approve/' + item.id, {
                approvedBy: vm.currentUser.name,
                approvedById: vm.currentUser.id,
                approvedByEmail: vm.currentUser.email,
                comments: comment
            }).then(function (response) {
                notificationsService.success("Success", response.data.message || "Content approved successfully");
                vm.loading = false;
                loadPendingContent();
            }).catch(function (error) {
                vm.loading = false;
                var errorMsg = error.data && error.data.error ? error.data.error : "Failed to approve content";
                notificationsService.error("Error", errorMsg);
            });
        }

        // Update the reject function in approval.controller.js
        // Alternative fix if you want to keep using the textbox view:

        // Replace the vm.reject function in your approval.controller.js with this:

        // Alternative fix if you want to keep using the textbox view:

        vm.reject = function (item) {
            if (vm.loading || !vm.currentUser) return;

            overlayService.open({
                title: "Reject Content - " + item.name,
                subtitle: "Please provide a reason for rejection",
                closeButtonLabel: "Cancel",
                submitButtonLabel: "Reject",
                submitButtonStyle: "danger",
                view: "views/propertyeditors/textbox/textbox.html",
                value: "", // Use value instead of textboxModel
                config: {
                    multiline: true,
                    maxlength: 500
                },
                submit: function (model) {
                    var comment = model.value; // Use model.value instead of model.textboxModel.value
                    if (!comment || comment.trim() === '') {
                        notificationsService.error("Error", "Rejection reason is required");
                        return;
                    }
                    performRejection(item, comment);
                    overlayService.close();
                },
                close: function () {
                    overlayService.close();
                }
            });
        };
        function performRejection(item, comment) {
            vm.loading = true;

            // Make sure we have current user
            if (!vm.currentUser) {
                notificationsService.error("Error", "User session expired. Please refresh and try again.");
                vm.loading = false;
                return;
            }

            // Include authentication headers like in loadPendingContent
            var config = {
                headers: {
                    'X-Current-User-Id': vm.currentUser.id.toString(),
                    'X-Current-User-Email': vm.currentUser.email,
                    'X-Current-User-Name': vm.currentUser.name,
                    'Content-Type': 'application/json'
                }
            };

            var requestData = {
                rejectedBy: vm.currentUser.name,
                rejectedById: vm.currentUser.id,
                rejectedByEmail: vm.currentUser.email,
                comments: comment // Required!
            };

            $http.post('/api/ContentApi/reject/' + item.id, requestData, config)
                .then(function (response) {
                    notificationsService.success("Success", response.data.message || "Content rejected");
                    vm.loading = false;
                    loadPendingContent();
                })
                .catch(function (error) {
                    vm.loading = false;
                    console.error("Rejection error:", error); // Add logging

                    // Handle different error types
                    if (error.status === 401 || error.status === 403) {
                        notificationsService.error("Authentication Error", "Your session may have expired. Please refresh the page.");
                    } else {
                        var errorMsg = error.data && error.data.error ? error.data.error : "Failed to reject content";
                        notificationsService.error("Error", errorMsg);
                    }
                });
        }

        vm.openContent = function (item) {
            editorService.contentEditor({
                id: item.id,
                create: false,
                close: function () {
                    editorService.close();
                    loadPendingContent();
                }
            });
        };

        init();
    })
    // Add safe HTML filter
    .filter('safe', ['$sce', function ($sce) {
        return function (text) {
            return $sce.trustAsHtml(text);
        };
    }]);