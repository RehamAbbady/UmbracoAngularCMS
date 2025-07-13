angular.module('umbraco').controller('ApprovalDashboardController', function ($scope, $http, userService, notificationsService, editorService) {
    var vm = this;
    vm.pendingItems = [];
    vm.loading = false;
    vm.error = null;

    function init() {
        loadPendingContent();
    }

    function loadPendingContent() {
        vm.loading = true;
        vm.error = null;

        // Use the public API endpoint that we know works
        $http.get('/api/ContentApi/pending-approvals').then(function (response) {
            vm.pendingItems = response.data;
            vm.loading = false;
        }).catch(function (error) {
            vm.loading = false;
            vm.error = "Failed to load pending items: " + (error.data ? error.data.error : error.statusText);
            console.error("Error loading pending approvals:", error);
        });
    }

    vm.approve = function (item) {
        if (vm.loading) return;

        vm.loading = true;

        userService.getCurrentUser().then(function (user) {
            $http.post('/api/ContentApi/approve/' + item.id, {
                approvedBy: user.name
            }).then(function (response) {
                notificationsService.success("Success", response.data.message || "Content approved successfully");
                vm.loading = false;
                loadPendingContent();
            }).catch(function (error) {
                vm.loading = false;
                var errorMsg = error.data && error.data.error ? error.data.error : "Failed to approve content";
                notificationsService.error("Error", errorMsg);
                console.error("Approval error:", error);
            });
        });
    };

    vm.reject = function (item) {
        if (vm.loading) return;

        vm.loading = true;

        userService.getCurrentUser().then(function (user) {
            $http.post('/api/ContentApi/reject/' + item.id, {
                rejectedBy: user.name
            }).then(function (response) {
                notificationsService.success("Success", response.data.message || "Content rejected");
                vm.loading = false;
                loadPendingContent();
            }).catch(function (error) {
                vm.loading = false;
                var errorMsg = error.data && error.data.error ? error.data.error : "Failed to reject content";
                notificationsService.error("Error", errorMsg);
                console.error("Rejection error:", error);
            });
        });
    };

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
});