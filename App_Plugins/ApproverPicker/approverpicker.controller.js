angular.module('umbraco').controller('ApproverPickerController', function ($scope, userService, $http) {
    var vm = this;

    if (!$scope.model.value) {
        $scope.model.value = {
            users: [],
            groups: [],
            approvalType: 'all',
            minimumApprovals: 1
        };
    }

    vm.users = [];
    vm.groups = [];
    vm.selectedUsers = $scope.model.value.users || [];
    vm.selectedGroups = $scope.model.value.groups || [];
    vm.approvalType = $scope.model.value.approvalType || 'all';
    vm.minimumApprovals = $scope.model.value.minimumApprovals || 1;

    // Load all users
    function loadUsers() {
        userService.getAll().then(function (data) {
            vm.users = data;
        });
    }

    // Load all groups
    function loadGroups() {
        $http.get('/umbraco/backoffice/api/users/GetAllUserGroups').then(function (response) {
            vm.groups = response.data;
        });
    }

    // Toggle user selection
    vm.toggleUser = function (user) {
        var index = vm.selectedUsers.findIndex(u => u.id === user.id);
        if (index > -1) {
            vm.selectedUsers.splice(index, 1);
        } else {
            vm.selectedUsers.push({
                id: user.id,
                name: user.name,
                email: user.email
            });
        }
        updateModel();
    };

    // Toggle group selection
    vm.toggleGroup = function (group) {
        var index = vm.selectedGroups.indexOf(group.alias);
        if (index > -1) {
            vm.selectedGroups.splice(index, 1);
        } else {
            vm.selectedGroups.push(group.alias);
        }
        updateModel();
    };

    // Check if user is selected
    vm.isUserSelected = function (user) {
        return vm.selectedUsers.some(u => u.id === user.id);
    };

    // Check if group is selected
    vm.isGroupSelected = function (group) {
        return vm.selectedGroups.indexOf(group.alias) > -1;
    };

    // Update model
    function updateModel() {
        $scope.model.value = {
            users: vm.selectedUsers,
            groups: vm.selectedGroups,
            approvalType: vm.approvalType,
            minimumApprovals: vm.minimumApprovals
        };
    }

    // Watch for changes
    $scope.$watch('vm.approvalType', updateModel);
    $scope.$watch('vm.minimumApprovals', updateModel);

    // Initialize
    loadUsers();
    loadGroups();
});