angular.module('umbraco').controller('WriterDashboardController', function($scope, $http, userService) {
    var vm = this;
    vm.myContent = [];
    vm.filter = 'all'; // all, pending, approved, rejected
    
    function loadMyContent() {
        userService.getCurrentUser().then(function(user) {
            $http.get('/api/ContentApi/my-content?userId=' + user.id)
                .then(function(response) {
                    vm.myContent = response.data;
                    categorizeContent();
                });
        });
    }
    
    function categorizeContent() {
        vm.pendingCount = vm.myContent.filter(c => c.status === 'Pending Approval').length;
        vm.approvedCount = vm.myContent.filter(c => c.status === 'Approved').length;
        vm.rejectedCount = vm.myContent.filter(c => c.status === 'Rejected').length;
    }
    
    vm.filterContent = function(status) {
        vm.filter = status;
    };
    
    vm.getFilteredContent = function() {
        if (vm.filter === 'all') return vm.myContent;
        return vm.myContent.filter(c => c.status.toLowerCase().includes(vm.filter));
    };
    
    vm.viewRejectionDetails = function(item) {
        // Show modal with rejection comments
        vm.selectedItem = item;
        vm.showRejectionModal = true;
    };
    
    loadMyContent();
});