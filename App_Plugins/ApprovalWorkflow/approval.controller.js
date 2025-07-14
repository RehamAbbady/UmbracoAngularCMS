angular.module('umbraco').controller('ApprovalDashboardController', function ($scope, $http, userService, notificationsService, editorService) {
    var vm = this;
    vm.pendingItems = [];
    vm.loading = false;
    vm.error = null;
    vm.currentUser = null;

    function init() {
        // Get current user first
        getCurrentUser().then(function () {
            loadPendingContent();
        });
    }

    function getCurrentUser() {
        return userService.getCurrentUser().then(function (user) {
            vm.currentUser = user;
            console.log("Current user:", user);
        });
    }

    function loadPendingContent() {
        vm.loading = true;
        vm.error = null;

        // Pass current user info in the request
        var config = {
            headers: {
                'X-Current-User-Id': vm.currentUser.id.toString(),
                'X-Current-User-Email': vm.currentUser.email,
                'X-Current-User-Name': vm.currentUser.name
            }
        };

        $http.get('/api/ContentApi/pending-approvals', config).then(function (response) {
            vm.pendingItems = response.data.items || response.data;
            vm.loading = false;
        }).catch(function (error) {
            vm.loading = false;
            vm.error = "Failed to load pending items: " + (error.data ? error.data.error : error.statusText);
            console.error("Error loading pending approvals:", error);
        });
    }

    vm.approve = function (item) {
        if (vm.loading || !vm.currentUser) return;

        vm.loading = true;

        $http.post('/api/ContentApi/approve/' + item.id, {
            approvedBy: vm.currentUser.name,
            approvedById: vm.currentUser.id,
            approvedByEmail: vm.currentUser.email
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
    };

    vm.reject = function (item) {
        if (vm.loading || !vm.currentUser) return;

        vm.loading = true;

        $http.post('/api/ContentApi/reject/' + item.id, {
            rejectedBy: vm.currentUser.name,
            rejectedById: vm.currentUser.id,
            rejectedByEmail: vm.currentUser.email
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